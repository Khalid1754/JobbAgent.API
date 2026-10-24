using System.Text.Json;
using JobbAgent.Api.Agents;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;

namespace JobbAgent.Api.Services;

// Styr körningarna. Varje steg sparas och skickas live till webbläsaren.
//   Sökning: Sökare → Matchare (rankar annonserna)
//   Brev:    Tolkare → Matchare → Skribent → Granskare, och tillbaka till Skribent om Granskaren
//            underkänner brevet (högst MaxRevisions gånger).
public class RunOrchestrator(
    IServiceScopeFactory scopes, RunEvents events,
    Searcher searcher, Interpreter interpreter, Matcher matcher, Writer writer, Reviewer reviewer,
    ILogger<RunOrchestrator> logger)
{
    public const int MaxRevisions = 1;

    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobbAgentDbContext>();
        var run = await db.Runs.FindAsync([runId], ct);
        if (run is null || run.Status != RunStatus.Queued) return;

        var log = new StepLog(db, run, events);
        await log.SetStatusAsync(RunStatus.Running, ct);
        var current = run.Kind == RunKind.Search ? Searcher.Name : Interpreter.Name;
        try
        {
            var cv = (await db.Profiles.FindAsync([1], ct))?.CvText;
            if (string.IsNullOrWhiteSpace(cv))
                throw new AgentException("Lägg in ditt CV först.");

            if (run.Kind == RunKind.Search)
                await SearchAsync(run, cv, log, a => current = a, ct);
            else
                await LetterAsync(run, cv, log, a => current = a, ct);

            await log.SetStatusAsync(RunStatus.Completed, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "{Agent} misslyckades i körning {RunId}.", current, runId);
            var message = e is AgentException ? e.Message : "Något gick fel på servern.";
            if (message.Length > 500) message = message[..500];
            await log.AddAsync(current, StepKind.Failed, message, ct: CancellationToken.None);
            run.Error = message;
            await log.SetStatusAsync(RunStatus.Failed, CancellationToken.None);
        }
    }

    private async Task SearchAsync(Run run, string cv, StepLog log, Action<string> setCurrent, CancellationToken ct)
    {
        setCurrent(Searcher.Name);
        await log.AddAsync(Searcher.Name, StepKind.Started, "Läser ditt CV och söker i Platsbanken", ct: ct);
        var result = await searcher.SearchAsync(cv, run.Location, run.Keywords, run.RemoteOnly, ct);
        await log.AddAsync(Searcher.Name, StepKind.Completed,
            $"Hittade {result.Ads.Count} annonser med {result.Queries.Count} sökningar",
            $"{result.Plan.Summary}\n\n**Sökningar:** {string.Join(", ", result.Queries.Select(q => $"\"{q}\""))}", ct: ct);
        if (result.Ads.Count == 0)
        {
            run.HitsJson = "[]";
            return;
        }
        await log.AddAsync(Searcher.Name, StepKind.Handoff, $"Lämnar över {result.Ads.Count} annonser", to: Matcher.Name, ct: ct);

        setCurrent(Matcher.Name);
        await log.AddAsync(Matcher.Name, StepKind.Started, "Jämför annonserna med ditt CV", ct: ct);
        var hits = await matcher.RankAsync(cv, result.Ads, ct);
        run.HitsJson = JsonSerializer.Serialize(hits, ApiJson.Options);
        run.MatchScore = hits.FirstOrDefault()?.Score;
        await log.AddAsync(Matcher.Name, StepKind.Completed,
            $"Bästa matchningen: {hits[0].Score} % ({hits[0].Title})", Matcher.FormatHits(hits.Take(10).ToList()), ct: ct);
    }

    private async Task LetterAsync(Run run, string cv, StepLog log, Action<string> setCurrent, CancellationToken ct)
    {
        setCurrent(Interpreter.Name);
        await log.AddAsync(Interpreter.Name, StepKind.Started, "Läser annonsen", ct: ct);
        var requirements = await interpreter.InterpretAsync(run.AdText ?? "", ct);
        run.RequirementsJson = JsonSerializer.Serialize(requirements, ApiJson.Options);
        if (requirements.Title.Length > 0) run.Title = Text.Truncate(requirements.Title, 300);
        if (requirements.Company.Length > 0) run.Company = Text.Truncate(requirements.Company, 200);
        if (run.Language == "auto") run.Language = requirements.Language == "en" ? "en" : "sv";
        await log.AddAsync(Interpreter.Name, StepKind.Completed,
            $"{requirements.MustHave.Length} krav och {requirements.NiceToHave.Length} meriter", Interpreter.Format(requirements), ct: ct);
        await log.AddAsync(Interpreter.Name, StepKind.Handoff, "Lämnar över kraven", to: Matcher.Name, ct: ct);

        setCurrent(Matcher.Name);
        await log.AddAsync(Matcher.Name, StepKind.Started, "Jämför kraven med ditt CV", ct: ct);
        var match = await matcher.MatchAsync(cv, requirements, ct);
        run.MatchJson = JsonSerializer.Serialize(match, ApiJson.Options);
        run.MatchScore = match.Score;
        await log.AddAsync(Matcher.Name, StepKind.Completed,
            $"Du matchar {match.Score} % ({match.Matches.Length} krav uppfyllda, {match.Gaps.Length} saknas)", Matcher.Format(match), ct: ct);
        await log.AddAsync(Matcher.Name, StepKind.Handoff, "Lämnar över vad brevet ska lyfta fram", to: Writer.Name, ct: ct);

        string? letter = null;
        string? feedback = null;
        for (var revision = 0; ; revision++)
        {
            setCurrent(Writer.Name);
            await log.AddAsync(Writer.Name, StepKind.Started,
                feedback is null ? "Skriver det personliga brevet" : "Skriver om brevet efter granskningen", ct: ct);
            letter = await writer.WriteAsync(cv, requirements, match, run.Language, letter, feedback, ct);
            await log.AddAsync(Writer.Name, StepKind.Completed,
                feedback is null ? "Första utkastet är klart" : "Ny version är klar", letter, ct: ct);
            await log.AddAsync(Writer.Name, StepKind.Handoff, "Lämnar över brevet för granskning", to: Reviewer.Name, ct: ct);

            setCurrent(Reviewer.Name);
            await log.AddAsync(Reviewer.Name, StepKind.Started, "Kontrollerar brevet mot CV:t och annonsen", ct: ct);
            var review = await reviewer.ReviewAsync(cv, requirements, run.Language, letter, ct);
            if (review.Approved || revision >= MaxRevisions)
            {
                await log.AddAsync(Reviewer.Name, StepKind.Completed,
                    review.Approved ? "Brevet är godkänt" : "Godkänt med anmärkningar (max antal omskrivningar nått)",
                    string.IsNullOrWhiteSpace(review.Feedback) ? null : review.Feedback, ct: ct);
                break;
            }
            feedback = review.Feedback;
            await log.AddAsync(Reviewer.Name, StepKind.Completed, "Brevet behöver förbättras", feedback, ct: ct);
            await log.AddAsync(Reviewer.Name, StepKind.Handoff, "Skickar tillbaka till Skribenten med synpunkter", to: Writer.Name, ct: ct);
        }
        run.Letter = letter;
    }

    private sealed class StepLog(JobbAgentDbContext db, Run run, RunEvents events)
    {
        private int _sequence = run.Steps.Count;

        public async Task AddAsync(string agent, StepKind kind, string summary, string? detail = null, string? to = null,
            CancellationToken ct = default)
        {
            var step = new AgentStep
            {
                RunId = run.Id, Sequence = ++_sequence, Agent = agent, ToAgent = to, Kind = kind,
                Summary = summary.Length > 500 ? summary[..500] : summary, Detail = detail,
            };
            db.Steps.Add(step);
            await db.SaveChangesAsync(ct);
            events.Publish(run.Id, RunEvent.ForStep(step));
        }

        public async Task SetStatusAsync(RunStatus status, CancellationToken ct)
        {
            run.Status = status;
            if (status is RunStatus.Completed or RunStatus.Failed) run.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            events.Publish(run.Id, RunEvent.ForStatus(run));
        }
    }
}
