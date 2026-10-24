using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;
using static JobbAgent.Api.Services.Language;

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
            if (await Cvs.CheckAsync(db, request.CvId, ct) is { } cvError)
                return Results.BadRequest(new { error = cvError });
            var location = Clean(request.Location, 100);
            var keywords = Clean(request.Keywords, 200);
            return await EnqueueAsync(db, queue, new Run
            {
                Kind = RunKind.Search, Title = Watches.Title(keywords, location, request.RemoteOnly),
                Location = location, Keywords = keywords, RemoteOnly = request.RemoteOnly, CvId = request.CvId,
            }, ct);
        });

        api.MapPost("/letters", async (CreateLetterRequest request, JobbAgentDbContext db, RunQueue queue,
            IJobBoard board, AdReader reader, CancellationToken ct) =>
        {
            if (await Cvs.CheckAsync(db, request.CvId, ct) is { } cvError)
                return Results.BadRequest(new { error = cvError });
            var language = request.Language ?? "auto";
            if (!Languages.Contains(language))
                return Results.BadRequest(new { error = L("Språket måste vara auto, sv eller en.", "The language must be auto, sv or en.") });

            var run = new Run { Kind = RunKind.Letter, Title = L("Ny ansökan", "New application"), Language = language, CvId = request.CvId };
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
                return Results.BadRequest(new { error = L($"Annonsen är för kort (minst {MinAdLength} tecken). Klistra in hela annonstexten.", $"The ad is too short (at least {MinAdLength} characters). Paste the whole ad text.") });
            if (run.AdText.Length > MaxAdLength)
                run.AdText = run.AdText[..MaxAdLength];
            return await EnqueueAsync(db, queue, run, ct);
        });

        api.MapPost("/interviews", async (CreateInterviewRequest request, JobbAgentDbContext db, RunQueue queue,
            IJobBoard board, AdReader reader, CancellationToken ct) =>
        {
            if (await Cvs.CheckAsync(db, request.CvId, ct) is { } cvError)
                return Results.BadRequest(new { error = cvError });

            var application = request.ApplicationId is { } appId ? await db.Applications.FirstOrDefaultAsync(a => a.Id == appId, ct) : null;
            if (request.ApplicationId is not null && application is null)
                return Results.BadRequest(new { error = L("Jobbet finns inte på tavlan.", "The job is not on the board.") });
            var letterId = request.LetterRunId ?? application?.LetterRunId;
            var letter = letterId is { } id ? await db.Runs.FirstOrDefaultAsync(r => r.Id == id && r.Kind == RunKind.Letter, ct) : null;
            if (request.LetterRunId is not null && letter is null)
                return Results.BadRequest(new { error = L("Brevet finns inte.", "The letter does not exist.") });
            application ??= letter is null ? null : await db.Applications.FirstOrDefaultAsync(a => a.LetterRunId == letter.Id, ct);

            var run = new Run { Kind = RunKind.Interview, SourceRunId = letter?.Id, ApplicationId = application?.Id, CvId = request.CvId ?? letter?.CvId };
            if (letter is { Status: RunStatus.Completed })
            {
                // Kraven och matchningen från brevet återanvänds, så att bara Intervjucoachen behöver jobba.
                run.Title = letter.Title;
                run.Company = letter.Company;
                run.AdUrl = letter.AdUrl;
                run.AdText = letter.AdText;
                run.PlatsbankenId = letter.PlatsbankenId;
                run.Language = letter.Language;
                run.RequirementsJson = letter.RequirementsJson;
                run.MatchJson = letter.MatchJson;
                run.MatchScore = letter.MatchScore;
            }
            else if (application is not null)
            {
                run.Title = application.Title;
                run.Company = application.Company;
                try
                {
                    var platsbankenId = application.PlatsbankenId ?? (application.Url is null ? null : AdReader.PlatsbankenId(application.Url));
                    if (platsbankenId is not null)
                    {
                        var ad = await GetAdAsync(board, platsbankenId, ct);
                        run.PlatsbankenId = ad.Id;
                        run.AdUrl = ad.Url;
                        run.AdText = $"{ad.Title}\n{ad.Employer}\n{ad.Municipality}\n\n{ad.Description}";
                    }
                    else if (application.Url is not null)
                    {
                        run.AdUrl = application.Url;
                        run.AdText = await reader.ReadAsync(application.Url, ct);
                    }
                    else
                    {
                        return Results.BadRequest(new { error = L("Lägg in en länk till annonsen på jobbet först, så att agenterna kan läsa den.", "Add a link to the ad on the job first, so the agents can read it.") });
                    }
                }
                catch (DocumentException e)
                {
                    return Results.BadRequest(new { error = e.Message });
                }
            }
            else
            {
                return Results.BadRequest(new { error = L("Välj ett brev eller ett jobb på tavlan.", "Choose a letter or a job on the board.") });
            }
            if (run.AdText is { Length: > MaxAdLength }) run.AdText = run.AdText[..MaxAdLength];
            run.Title = Text.Truncate(L($"Intervju: {run.Title}", $"Interview: {run.Title}"), 300);
            return await EnqueueAsync(db, queue, run, ct);
        });

        var runs = api.MapGroup("/runs");

        runs.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            await db.Runs.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Take(100)
                .Select(r => new RunSummaryDto(r.Id, r.Kind, r.Title, r.Company, r.Status, r.MatchScore, r.SourceRunId, r.ApplicationId, r.WatchId, r.CreatedAt, r.FinishedAt))
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
                return Results.Conflict(new { error = L("Brevet kan ändras först när agenterna är klara.", "The letter can be changed once the agents are done.") });
            var letter = (request.Letter ?? "").Replace("\r", "").Trim();
            if (letter.Length is 0 or > MaxLetterLength)
                return Results.BadRequest(new { error = L($"Brevet måste vara 1–{MaxLetterLength} tecken.", $"The letter must be 1–{MaxLetterLength} characters.") });
            run.Letter = letter;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        // Brevet som Word eller PDF, med dina kontaktuppgifter överst.
        runs.MapGet("/{id:guid}/letter.{format:regex(^(docx|pdf)$)}", async (Guid id, string format, JobbAgentDbContext db,
            IConfiguration config, CancellationToken ct) =>
        {
            var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.Kind == RunKind.Letter, ct);
            if (run?.Letter is null) return Results.NotFound();
            var profile = await Cvs.ResolveAsync(db, run.CvId, ct);
            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Watches.Zone(config)).Date;
            var letter = LetterContent.From(run, profile, today);
            return format == "pdf"
                ? Results.File(LetterDocument.Pdf(letter), "application/pdf", letter.FileName("pdf"))
                : Results.File(LetterDocument.Docx(letter), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", letter.FileName("docx"));
        });

        runs.MapDelete("/{id:guid}", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var run = await db.Runs.FirstOrDefaultAsync(r => r.Id == id, ct);
            if (run is null) return Results.NotFound();
            if (run.Status == RunStatus.Running)
                return Results.Conflict(new { error = L("Agenterna jobbar fortfarande, vänta tills de är klara.", "The agents are still working, wait until they are done.") });
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

    internal static async Task<IResult> EnqueueAsync(JobbAgentDbContext db, RunQueue queue, Run run, CancellationToken ct)
    {
        run.UiLanguage = Language.Current.Code();
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
                ?? throw new DocumentException(L("Annonsen finns inte längre i Platsbanken.", "The ad is no longer in Platsbanken."));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            throw new DocumentException(L("Kunde inte hämta annonsen från Platsbanken. Försök igen eller klistra in texten.", "Could not fetch the ad from Platsbanken. Try again or paste the text."));
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
