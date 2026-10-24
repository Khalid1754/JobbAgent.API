using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using JobbAgent.Api.Services;

namespace JobbAgent.Api.Tests;

// Svarar som respektive agent utifrån vem systemprompten vänder sig till.
public class FakeLlm : ILlm
{
    public static readonly string[] Agents = ["Sökare", "Tolkare", "Matchare", "Skribent", "Granskare", "Intervjucoach"];

    public List<string> Calls { get; } = [];
    public List<string> Prompts { get; } = [];
    public Queue<string> ReviewAnswers { get; } = new();
    public string? FailFor { get; set; }

    public bool Enabled => true;

    public Task<string> CompleteAsync(string system, string prompt, JsonObject? schema, CancellationToken ct)
    {
        var agent = Agents.First(a => system.Contains($"You are {a}"));
        lock (Calls)
        {
            Calls.Add(agent);
            Prompts.Add(prompt);
        }
        if (agent == FailFor) throw new LlmException($"{agent} gick sönder");
        return Task.FromResult(agent switch
        {
            "Sökare" => """{"queries":["systemutvecklare","backendutvecklare"],"summary":"Utvecklarjobb med C#."}""",
            "Matchare" when system.Contains("each job ad") =>
                """{"ads":[{"index":1,"score":40,"reason":"Delvis."},{"index":2,"score":90,"reason":"Passar bra."}]}""",
            "Matchare" => """{"score":82,"summary":"Bra match.","matches":[{"requirement":"C#","evidence":"Tre år med C#"}],"gaps":["Azure"],"highlights":["C#"]}""",
            "Tolkare" => """
                {"title":"Backendutvecklare","company":"Acme AB","location":"Stockholm","summary":"Bygga API:er.",
                 "mustHave":["C#"],"niceToHave":["Azure"],"responsibilities":["API"],"traits":["Noggrann"],
                 "contactPerson":"Anna","language":"sv"}
                """,
            "Intervjucoach" => """
                {"summary":"Lyft fram dina projekt.","questions":[{"category":"Kompetens","question":"Berätta om ett API du byggt.",
                 "whyTheyAsk":"De vill se erfarenhet.","tips":["Nämn TrAInr"],"exampleAnswer":"Jag byggde..."}],
                 "questionsToAsk":["Hur ser teamet ut?"],"gaps":[{"gap":"Azure","howToHandle":"Visa att du lär dig snabbt."}]}
                """,
            "Skribent" => prompt.Contains("Granskare's feedback") ? "Hej Anna! Brev v2" : "Hej Anna! Brev v1",
            _ => ReviewAnswers.Count > 0 ? ReviewAnswers.Dequeue() : """{"approved":true,"feedback":""}""",
        });
    }
}

public class FakeJobBoard : IJobBoard
{
    public static JobAd Ad(string id, string title) =>
        new(id, title, "Acme AB", "Stockholm", "Utvecklare", $"Vi söker en {title} som kan C#. " + new string('x', 200),
            $"https://arbetsformedlingen.se/platsbanken/annonser/{id}", null, null);

    public List<(string Query, bool Remote)> Searches { get; } = [];
    public bool Fail { get; set; }

    public Task<IReadOnlyList<JobAd>> SearchAsync(string query, bool remoteOnly, int limit, CancellationToken ct)
    {
        if (Fail) throw new HttpRequestException("nere");
        lock (Searches) Searches.Add((query, remoteOnly));
        return Task.FromResult<IReadOnlyList<JobAd>>([Ad("1", "Systemutvecklare"), Ad("2", "Backendutvecklare")]);
    }

    public Task<JobAd?> GetAsync(string id, CancellationToken ct) =>
        Task.FromResult(id == "404" ? null : Ad(id, "Backendutvecklare"));
}

public sealed class FakeHandler(HttpStatusCode status, string body, string contentType = "application/json") : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? RequestBody { get; private set; }
    public int Calls { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };
    }
}
