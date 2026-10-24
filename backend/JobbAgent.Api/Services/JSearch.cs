using System.Text.Json.Nodes;

namespace JobbAgent.Api.Services;

// JSearch (via RapidAPI) söker i Google for Jobs, som samlar annonser från LinkedIn, Indeed, Glassdoor och
// arbetsgivarnas egna sidor. Kräver en gratis nyckel (RAPIDAPI_KEY): https://rapidapi.com/letscrape-6bRBa3QguO5/api/jsearch
// Annonserna får id "js:<job_id>" så att de inte blandas ihop med Platsbankens.
public class JSearchClient(HttpClient http, IConfiguration config) : IJobBoard
{
    public const string Prefix = "js:";
    private const string Host = "jsearch.p.rapidapi.com";

    private readonly string? _apiKey = new[] { config["JSearch:ApiKey"], Environment.GetEnvironmentVariable("RAPIDAPI_KEY") }
        .FirstOrDefault(k => !string.IsNullOrWhiteSpace(k));

    public bool Enabled => _apiKey is not null;
    private string BaseUrl => (config["JSearch:BaseUrl"] ?? $"https://{Host}").TrimEnd('/');
    private string Country => config["JSearch:Country"] ?? "se";

    public async Task<IReadOnlyList<JobAd>> SearchAsync(string query, bool remoteOnly, int limit, CancellationToken ct)
    {
        var url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&page=1&num_pages=1&country={Country}&date_posted=month";
        if (remoteOnly) url += "&work_from_home=true";
        var json = await GetJsonAsync(url, ct);
        return json?["data"] is JsonArray data
            ? data.OfType<JsonObject>().Select(Parse).OfType<JobAd>().Take(limit).ToList()
            : [];
    }

    public async Task<JobAd?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(Prefix)) return null;
        var json = await GetJsonAsync($"{BaseUrl}/job-details?job_id={Uri.EscapeDataString(id[Prefix.Length..])}&country={Country}", ct);
        return json?["data"] is JsonArray { Count: > 0 } data && data[0] is JsonObject ad ? Parse(ad) : null;
    }

    private async Task<JsonNode?> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-RapidAPI-Key", _apiKey);
        request.Headers.Add("X-RapidAPI-Host", Host);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"JSearch svarade {(int)response.StatusCode}");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    internal static JobAd? Parse(JsonObject job)
    {
        var id = Text(job["job_id"]);
        var title = Text(job["job_title"]);
        var url = Text(job["job_apply_link"]) ?? Text(job["job_google_link"]);
        if (id is null || title is null || url is null) return null;
        return new JobAd(
            Prefix + id, title,
            Text(job["employer_name"]),
            Text(job["job_city"]) ?? Text(job["job_state"]),
            null,
            Text(job["job_description"]) ?? "",
            url,
            Date(job["job_offer_expiration_datetime_utc"]),
            Date(job["job_posted_at_datetime_utc"]),
            job["job_is_remote"]?.GetValueKind() == System.Text.Json.JsonValueKind.True,
            Text(job["job_publisher"]) ?? "JSearch");
    }

    private static string? Text(JsonNode? node) =>
        node?.GetValueKind() == System.Text.Json.JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } s ? s.Trim() : null;

    private static DateTime? Date(JsonNode? node) => Text(node) is { } s && DateTime.TryParse(s, out var d) ? d.ToUniversalTime() : null;
}
