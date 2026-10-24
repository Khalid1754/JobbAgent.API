using System.Text.Json;
using JobbAgent.Api.Services;

namespace JobbAgent.Api.Agents;

public record SearchResult(SearchPlan Plan, IReadOnlyList<string> Queries, IReadOnlyList<JobAd> Ads);

// Sökare: läser CV:t, tar fram sökfraser och hämtar annonser från Platsbanken.
public class Searcher(ILlm llm, IJobBoard board, IConfiguration config, ILogger<Searcher> logger)
{
    public const string Name = "Sökare";

    private const string System = """
        You are Sökare in a team of AI agents that help a job seeker in Sweden. Read the CV and the
        wishes, and suggest 1–3 short search phrases (Swedish job titles work best, optionally one key
        skill) for Arbetsförmedlingen's Platsbanken that would find jobs this person is qualified for.
        Do not include the location; it is added separately.
        """;

    public int MaxAds => config.GetValue("Search:MaxAds", 15);

    public async Task<SearchResult> SearchAsync(string cv, string? location, string? keywords, bool remoteOnly, CancellationToken ct)
    {
        var plan = await llm.CompleteJsonAsync<SearchPlan>(System, $"""
            CV:
            {Text.Truncate(cv, 6000)}

            Wishes: {(string.IsNullOrWhiteSpace(keywords) ? "none" : keywords)}
            """, ct);

        var queries = plan.Queries.Select(q => q.Trim()).Where(q => q.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(3)
            .Select(q => string.IsNullOrWhiteSpace(location) || q.Contains(location, StringComparison.OrdinalIgnoreCase) ? q : $"{q} {location.Trim()}")
            .ToList();
        if (queries.Count == 0)
            throw new AgentException("Sökaren kunde inte ta fram några sökord ur CV:t. Lägg till egna sökord och försök igen.");

        var perQuery = new List<IReadOnlyList<JobAd>>();
        var failures = 0;
        foreach (var query in queries)
        {
            try
            {
                perQuery.Add(await board.SearchAsync(query, remoteOnly, MaxAds, ct));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "Platsbanken-sökningen \"{Query}\" misslyckades.", query);
                failures++;
            }
        }
        if (failures == queries.Count)
            throw new AgentException("Kunde inte nå Platsbanken. Kontrollera internetanslutningen och försök igen.");

        return new SearchResult(plan, queries, Interleave(perQuery, MaxAds));
    }

    // Varvar träffarna från sökningarna så att alla sökfraser får plats, och tar bort dubbletter.
    internal static List<JobAd> Interleave(IReadOnlyList<IReadOnlyList<JobAd>> lists, int max)
    {
        var result = new List<JobAd>();
        var seen = new HashSet<string>();
        for (var i = 0; result.Count < max && lists.Any(l => i < l.Count); i++)
            foreach (var list in lists)
                if (i < list.Count && result.Count < max && seen.Add(list[i].Id))
                    result.Add(list[i]);
        return result;
    }
}
