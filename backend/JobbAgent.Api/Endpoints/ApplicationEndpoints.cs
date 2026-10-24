using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobbAgent.Api.Endpoints;

// Ansökningstavlan: jobb som du sparat, sökt eller fått intervju på.
public static class ApplicationEndpoints
{
    public const int MaxNotesLength = 4000;

    public static void MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var applications = app.MapGroup("/api/applications");

        applications.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            (await db.Applications.AsNoTracking().OrderByDescending(a => a.StageChangedAt).ToListAsync(ct))
                .Select(ApplicationDto.From));

        applications.MapPost("/", async (CreateApplicationRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            Run? letter = null;
            if (request.LetterRunId is { } runId)
            {
                letter = await db.Runs.FirstOrDefaultAsync(r => r.Id == runId && r.Kind == RunKind.Letter, ct);
                if (letter is null) return Results.BadRequest(new { error = "Brevet finns inte." });
            }
            var platsbankenId = Clean(request.PlatsbankenId, 50) ?? letter?.PlatsbankenId;

            // Samma jobb ska bara finnas en gång på tavlan. Sparade du annonsen från en sökning och
            // skriver brev till den senare, kopplas brevet till samma kort.
            var existing = await db.Applications.FirstOrDefaultAsync(a =>
                (letter != null && a.LetterRunId == letter.Id) || (platsbankenId != null && a.PlatsbankenId == platsbankenId), ct);
            if (existing is not null)
            {
                existing.LetterRunId ??= letter?.Id;
                existing.MatchScore = letter?.MatchScore ?? existing.MatchScore;
                if (request.Stage is { } stage && stage > existing.Stage) SetStage(existing, stage);
                await db.SaveChangesAsync(ct);
                return Results.Ok(ApplicationDto.From(existing));
            }

            var application = new JobApplication
            {
                Title = Clean(request.Title, 300) ?? letter?.Title ?? "",
                Company = Clean(request.Company, 200) ?? letter?.Company,
                Location = Clean(request.Location, 100),
                Url = Clean(request.Url, 1000) ?? letter?.AdUrl,
                PlatsbankenId = platsbankenId,
                Deadline = request.Deadline,
                MatchScore = request.MatchScore is { } score ? Math.Clamp(score, 0, 100) : letter?.MatchScore,
                LetterRunId = letter?.Id,
            };
            if (application.Title.Length == 0)
                return Results.BadRequest(new { error = "Ange jobbets titel." });
            SetStage(application, request.Stage ?? Stage.Saved);

            db.Applications.Add(application);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/applications/{application.Id}", ApplicationDto.From(application));
        });

        applications.MapPut("/{id:guid}", async (Guid id, UpdateApplicationRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (application is null) return Results.NotFound();
            var title = Clean(request.Title, 300);
            if (title is null) return Results.BadRequest(new { error = "Ange jobbets titel." });
            if (request.Notes?.Length > MaxNotesLength)
                return Results.BadRequest(new { error = $"Anteckningarna får vara högst {MaxNotesLength} tecken." });

            application.Title = title;
            application.Company = Clean(request.Company, 200);
            application.Location = Clean(request.Location, 100);
            application.Url = Clean(request.Url, 1000);
            application.Deadline = request.Deadline;
            application.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            application.AppliedAt = request.AppliedAt;
            application.InterviewAt = request.InterviewAt;
            SetStage(application, request.Stage);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ApplicationDto.From(application));
        });

        applications.MapDelete("/{id:guid}", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, ct);
            if (application is null) return Results.NotFound();
            db.Applications.Remove(application);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    // Byter steg och kommer ihåg när. Första gången jobbet markeras som sökt sparas datumet.
    internal static void SetStage(JobApplication application, Stage stage)
    {
        if (application.Stage == stage) return;
        if (application.Stage == Stage.Saved) application.AppliedAt ??= DateTime.UtcNow;
        application.Stage = stage;
        application.StageChangedAt = DateTime.UtcNow;
    }

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Text.Truncate(value.Trim(), max);
}
