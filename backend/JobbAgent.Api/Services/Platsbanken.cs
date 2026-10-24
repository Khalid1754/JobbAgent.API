using System.Text.Json.Nodes;

namespace JobbAgent.Api.Services;

// En jobbannons, i den form agenterna behöver.
public record JobAd(
    string Id, string Title, string? Employer, string? Municipality, string? Occupation,
    string Description, string Url, DateTime? Deadline, DateTime? Published, bool Remote = false, string Source = "Platsbanken");

public interface IJobBoard
{
    Task<IReadOnlyList<JobAd>> SearchAsync(string query, bool remoteOnly, int limit, CancellationToken ct);
    Task<JobAd?> GetAsync(string id, CancellationToken ct);
}

// Arbetsförmedlingens öppna JobSearch-API (samma annonser som Platsbanken). Gratis och utan nyckel:
// https://jobsearch.api.jobtechdev.se
public class PlatsbankenClient(HttpClient http, IConfiguration config) : IJobBoard
{
    private string BaseUrl => (config["Platsbanken:BaseUrl"] ?? "https://jobsearch.api.jobtechdev.se").TrimEnd('/');

    public async Task<IReadOnlyList<JobAd>> SearchAsync(string query, bool remoteOnly, int limit, CancellationToken ct)
    {
        var url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (remoteOnly) url += "&remote=true";
        var json = await GetJsonAsync(url, ct);
        return json?["hits"] is JsonArray hits
            ? hits.OfType<JsonObject>().Select(Parse).Where(a => a is not null).Select(a => a!).ToList()
            : [];
    }

    public async Task<JobAd?> GetAsync(string id, CancellationToken ct) =>
        await GetJsonAsync($"{BaseUrl}/ad/{Uri.EscapeDataString(id)}", ct) is JsonObject ad ? Parse(ad) : null;

    private async Task<JsonNode?> GetJsonAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Platsbanken svarade {(int)response.StatusCode}");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    internal static JobAd? Parse(JsonObject ad)
    {
        var id = ad["id"]?.ToString();
        var title = Text(ad["headline"]);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title)) return null;
        return new JobAd(
            id, title,
            Text(ad["employer"]?["name"]),
            Text(ad["workplace_address"]?["municipality"]) ?? Text(ad["workplace_address"]?["region"]),
            Text(ad["occupation"]?["label"]),
            Text(ad["description"]?["text"]) ?? "",
            Text(ad["webpage_url"]) ?? $"https://arbetsformedlingen.se/platsbanken/annonser/{id}",
            Date(ad["application_deadline"]),
            Date(ad["publication_date"]),
            ad["remote_work"]?.GetValueKind() == System.Text.Json.JsonValueKind.True);
    }

    private static string? Text(JsonNode? node) =>
        node?.GetValueKind() == System.Text.Json.JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } s ? s.Trim() : null;

    private static DateTime? Date(JsonNode? node) => Text(node) is { } s && DateTime.TryParse(s, out var d) ? d : null;
}
