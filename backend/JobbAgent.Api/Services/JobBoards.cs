namespace JobbAgent.Api.Services;

// Söker i alla jobbkällor på en gång: alltid Platsbanken, och JSearch (LinkedIn, Indeed m.fl.) om nyckeln finns.
// Går en källa inte att nå används de andra, så att sökningen inte stannar för att t.ex. gratiskvoten är slut.
public class JobBoards(PlatsbankenClient platsbanken, JSearchClient jsearch, ILogger<JobBoards> logger) : IJobBoard
{
    public IReadOnlyList<string> Sources => jsearch.Enabled ? ["Platsbanken", "JSearch"] : ["Platsbanken"];

    private IEnumerable<IJobBoard> Boards => jsearch.Enabled ? [platsbanken, jsearch] : [platsbanken];

    public async Task<IReadOnlyList<JobAd>> SearchAsync(string query, bool remoteOnly, int limit, CancellationToken ct)
    {
        var tasks = Boards.Select(async board =>
        {
            try
            {
                return (Ads: await board.SearchAsync(query, remoteOnly, limit, ct), Error: (Exception?)null);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "{Board} kunde inte söka efter \"{Query}\".", board.GetType().Name, query);
                return (Ads: (IReadOnlyList<JobAd>)[], Error: e);
            }
        }).ToList();
        var results = await Task.WhenAll(tasks);
        if (results.All(r => r.Error is not null)) throw results[0].Error!;
        return Merge(results.Select(r => r.Ads).ToList(), limit);
    }

    public Task<JobAd?> GetAsync(string id, CancellationToken ct) =>
        id.StartsWith(JSearchClient.Prefix) ? jsearch.GetAsync(id, ct) : platsbanken.GetAsync(id, ct);

    // Varvar källorna och tar bort samma jobb som finns i flera (samma titel hos samma arbetsgivare).
    internal static List<JobAd> Merge(IReadOnlyList<IReadOnlyList<JobAd>> lists, int limit)
    {
        var result = new List<JobAd>();
        var seen = new HashSet<string>();
        for (var i = 0; result.Count < limit && lists.Any(l => i < l.Count); i++)
            foreach (var list in lists)
                if (i < list.Count && result.Count < limit && seen.Add(Key(list[i])))
                    result.Add(list[i]);
        return result;
    }

    private static string Key(JobAd ad) =>
        $"{Normalize(ad.Title)}|{Normalize(ad.Employer ?? "")}";

    private static string Normalize(string s) => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
