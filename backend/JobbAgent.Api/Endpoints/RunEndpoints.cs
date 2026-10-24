using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobbAgent.Api.Endpoints;

public static class RunEndpoints
{
    public const int MinAdLength = 100;
    public const int MaxAdLength = 20000;
    public const int MaxLetterLength = 10000;
    private static readonly string[] Languages = ["auto", "sv", "en"];

    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", async (ILlm llm, JobbAgentDbContext db, CancellationToken ct) =>
            new { aiEnabled = llm.Enabled, hasCv = await db.Profiles.AnyAsync(ct) });

        api.MapPost("/searches", async (CreateSearchRequest request, JobbAgentDbContext db, RunQueue queue, CancellationToken ct) =>
        {
            if (!await db.Profiles.AnyAsync(ct))
                return Results.BadRequest(new { error = "Lägg in ditt CV först." });
            var location = Clean(request.Location, 100);
            var keywords = Clean(request.Keywords, 200);
            var title = (keywords ?? "Jobb som passar mitt CV") + (location is null ? "" : $" i {location}") + (request.RemoteOnly ? " (distans)" : "");
            return await EnqueueAsync(db, queue, new Run
            {
                Kind = RunKind.Search, Title = Text.Truncate(title, 300),
                Location = location, Keywords = keywords, RemoteOnly = request.RemoteOnly,
            }, ct);
        });

        api.MapPost("/letters", async (CreateLetterRequest request, JobbAgentDbContext db, RunQueue queue,
            IJobBoard board, AdReader reader, CancellationToken ct) =>
        {
            if (!await db.Profiles.AnyAsync(ct))
                return Results.BadRequest(new { error = "Lägg in ditt CV först." });
            var language = request.Language ?? "auto";
            if (!Languages.Contains(language))
                return Results.BadRequest(new { error = "Språket måste vara auto, sv eller en." });

            var run = new Run { Kind = RunKind.Letter, Title = "Ny ansökan", Language = language };
            try
            {
                var url = Clean(request.AdUrl, 1000);
                var platsbankenId = Clean(request.PlatsbankenId, 50) ?? (url is null ? null : AdReader.PlatsbankenId(url));
                if (platsbankenId is not null)
                {
                    var ad = await GetAdAsync(board, platsbankenId, ct);
                    run.PlatsbankenId = ad.Id;
                    run.AdUrl = ad.Url;
                    run.Title = Text.Truncate(ad.Title, 300);
                    run.Company = ad.Employer is null ? null : Text.Truncate(ad.Employer, 200);
                    run.AdText = $"{ad.Title}\n{ad.Employer}\n{ad.Municipality}\n\n{ad.Description}";
                }
                else if (url is not null)
                {
                    run.AdUrl = url;
                    run.AdText = await reader.ReadAsync(url, ct);
                }
                else
                {
                    run.AdText = Text.Tidy(request.AdText ?? "");
                }
            }
            catch (DocumentException e)
            {
                return Results.BadRequest(new { error = e.Message });
            }

            if (run.AdText.Length < MinAdLength)
                return Results.BadRequest(new { error = $"Annonsen är för kort (minst {MinAdLength} tecken). Klistra in hela annonstexten." });
            if (run.AdText.Length > MaxAdLength)
                run.AdText = run.AdText[..MaxAdLength];
            return await EnqueueAsync(db, queue, run, ct);
        });

        var runs = api.MapGroup("/runs");

        runs.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            await db.Runs.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .Select(r => new RunSummaryDto(r.Id, r.Kind, r.Title, r.Company, r.Status, r.MatchScore, r.CreatedAt, r.FinishedAt))
                .ToListAsync(ct));

        runs.MapGet("/{id:guid}", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
            await db.Runs.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id, ct) is { } run
                ? Results.Ok(RunDto.From(run))
                : Results.NotFound());

        // Sparar ändringar du gjort i brevet.
        runs.MapPut("/{id:guid}/letter", async (Guid id, UpdateLetterRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run is null) return Results.NotFound();
            if (run.Kind != RunKind.Letter || run.Status != RunStatus.Completed)
                return Results.Conflict(new { error = "Brevet kan ändras först när agenterna är klara." });
            var letter = (request.Letter ?? "").Replace("\r", "").Trim();
            if (letter.Length is 0 or > MaxLetterLength)
                return Results.BadRequest(new { error = $"Brevet måste vara 1–{MaxLetterLength} tecken." });
            run.Letter = letter;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        runs.MapDelete("/{id:guid}", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run is null) return Results.NotFound();
            if (run.Status == RunStatus.Running)
                return Results.Conflict(new { error = "Agenterna jobbar fortfarande, vänta tills de är klara." });
            // Jobbet ligger kvar på tavlan även om brevet tas bort.
            await db.Applications.Where(a => a.LetterRunId == id).ForEachAsync(a => a.LetterRunId = null, ct);
            db.Runs.Remove(run);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Server-Sent Events: först allt som redan hänt, sedan nya steg live tills körningen är klar.
        runs.MapGet("/{id:guid}/events", (Guid id, JobbAgentDbContext db, RunEvents events, CancellationToken ct) =>
            TypedResults.ServerSentEvents(StreamAsync(id, db, events, ct)));
    }

    private static async Task<IResult> EnqueueAsync(JobbAgentDbContext db, RunQueue queue, Run run, CancellationToken ct)
    {
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        queue.Enqueue(run.Id);
        return Results.Created($"/api/runs/{run.Id}", RunSummaryDto.From(run));
    }

    private static async Task<JobAd> GetAdAsync(IJobBoard board, string id, CancellationToken ct)
    {
        try
        {
            return await board.GetAsync(id, ct)
                ?? throw new DocumentException("Annonsen finns inte längre i Platsbanken.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            throw new DocumentException("Kunde inte hämta annonsen från Platsbanken. Försök igen eller klistra in texten.");
        }
    }

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Text.Truncate(value.Trim(), max);

    internal static async IAsyncEnumerable<SseItem<string>> StreamAsync(Guid id, JobbAgentDbContext db, RunEvents events,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Prenumerera innan databasen läses, så att inget steg hamnar mellan stolarna.
        using var subscription = events.Subscribe(id);
        var run = await db.Runs.AsNoTracking().Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) yield break;

        var lastSequence = 0;
        foreach (var step in run.Steps.OrderBy(s => s.Sequence))
        {
            lastSequence = step.Sequence;
            yield return Item(RunEvent.ForStep(step));
        }
        yield return Item(RunEvent.ForStatus(run));
        if (IsFinished(run.Status)) yield break;

        await foreach (var e in subscription.Reader.ReadAllAsync(ct))
        {
            if (e.Step is { } s)
            {
                if (s.Sequence <= lastSequence) continue;
                lastSequence = s.Sequence;
            }
            yield return Item(e);
            if (e.Status is { } status && IsFinished(status)) yield break;
        }
    }

    private static bool IsFinished(RunStatus status) => status is RunStatus.Completed or RunStatus.Failed;

    private static SseItem<string> Item(RunEvent e) => new(JsonSerializer.Serialize(e, ApiJson.Options), e.Type);
}
