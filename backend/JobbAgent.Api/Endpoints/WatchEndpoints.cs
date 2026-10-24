using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace JobbAgent.Api.Endpoints;

// Bevakningar: sparade sökningar som körs automatiskt varje morgon.
public static class WatchEndpoints
{
    public const int MaxWatches = 10;

    public static void MapWatchEndpoints(this IEndpointRouteBuilder app)
    {
        var watches = app.MapGroup("/api/watches");

        watches.MapGet("/", async (JobbAgentDbContext db, CancellationToken ct) =>
            (await db.Watches.AsNoTracking().OrderBy(w => w.CreatedAt).ToListAsync(ct)).Select(WatchDto.From));

        watches.MapPost("/", async (SaveWatchRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            if (await db.Watches.CountAsync(ct) >= MaxWatches)
                return Results.BadRequest(new { error = $"Du kan ha högst {MaxWatches} bevakningar." });
            if (await Cvs.CheckAsync(db, request.CvId, ct) is { } cvError && request.CvId is not null)
                return Results.BadRequest(new { error = cvError });
            var watch = new Watch();
            Apply(watch, request);
            db.Watches.Add(watch);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/watches/{watch.Id}", WatchDto.From(watch));
        });

        watches.MapPut("/{id:guid}", async (Guid id, SaveWatchRequest request, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (watch is null) return Results.NotFound();
            if (await Cvs.CheckAsync(db, request.CvId, ct) is { } cvError && request.CvId is not null)
                return Results.BadRequest(new { error = cvError });
            Apply(watch, request);
            await db.SaveChangesAsync(ct);
            return Results.Ok(WatchDto.From(watch));
        });

        // Kör bevakningen direkt i stället för att vänta till i morgon bitti.
        watches.MapPost("/{id:guid}/run", async (Guid id, JobbAgentDbContext db, RunQueue queue, CancellationToken ct) =>
        {
            var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (watch is null) return Results.NotFound();
            if (await Cvs.CheckAsync(db, watch.CvId, ct) is { } cvError)
                return Results.BadRequest(new { error = cvError });
            watch.LastRunAt = DateTime.UtcNow;
            return await RunEndpoints.EnqueueAsync(db, queue, Watches.CreateRun(watch), ct);
        });

        // Du har tittat på de nya annonserna.
        watches.MapPost("/{id:guid}/seen", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (watch is null) return Results.NotFound();
            watch.NewMatches = 0;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        watches.MapDelete("/{id:guid}", async (Guid id, JobbAgentDbContext db, CancellationToken ct) =>
        {
            var watch = await db.Watches.FirstOrDefaultAsync(w => w.Id == id, ct);
            if (watch is null) return Results.NotFound();
            db.Watches.Remove(watch);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }

    private static void Apply(Watch watch, SaveWatchRequest request)
    {
        watch.Location = string.IsNullOrWhiteSpace(request.Location) ? null : Text.Truncate(request.Location.Trim(), 100);
        watch.Keywords = string.IsNullOrWhiteSpace(request.Keywords) ? null : Text.Truncate(request.Keywords.Trim(), 200);
        watch.RemoteOnly = request.RemoteOnly;
        watch.MinScore = Math.Clamp(request.MinScore ?? watch.MinScore, 0, 100);
        watch.Enabled = request.Enabled ?? watch.Enabled;
        watch.CvId = request.CvId;
    }
}
