using System.Text.Json;
using JobbAgent.Api.Agents;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using Microsoft.EntityFrameworkCore;
using static JobbAgent.Api.Services.Language;

namespace JobbAgent.Api.Services;

public static class Watches
{
    // Så många visade annonser kommer bevakningen ihåg; äldre annonser har ändå hunnit gå ut.
    public const int MaxSeen = 1000;

    public static string Title(string? keywords, string? location, bool remoteOnly) =>
        Text.Truncate((keywords ?? L("Jobb som passar mitt CV", "Jobs that fit my CV")) + (location is null ? "" : L($" i {location}", $" in {location}")) + (remoteOnly ? L(" (distans)", " (remote)") : ""), 300);

    public static Run CreateRun(Watch w) => new()
    {
        Kind = RunKind.Search, WatchId = w.Id, Title = Text.Truncate(L("Bevakning: ", "Saved search: ") + Title(w.Keywords, w.Location, w.RemoteOnly), 300),
        Location = w.Location, Keywords = w.Keywords, RemoteOnly = w.RemoteOnly, CvId = w.CvId, UiLanguage = w.UiLanguage,
    };

    // Markerar annonser som bevakningen inte visat förut och räknar hur många av dem som matchar bra.
    public static List<SearchHit> MarkNew(Watch watch, IReadOnlyList<SearchHit> hits, Guid runId)
    {
        var seen = JsonSerializer.Deserialize<List<string>>(watch.SeenAdIdsJson) ?? [];
        var seenSet = seen.ToHashSet();
        var marked = hits.Select(h => h with { IsNew = !seenSet.Contains(h.Id) }).ToList();
        seen.AddRange(marked.Where(h => h.IsNew).Select(h => h.Id));
        watch.SeenAdIdsJson = JsonSerializer.Serialize(seen.TakeLast(MaxSeen));
        watch.NewMatches = marked.Count(h => h.IsNew && h.Score >= watch.MinScore);
        watch.LastRunId = runId;
        return marked;
    }

    // Ska bevakningen köras nu? En gång per dag, från klockan Watch:Hour lokal tid.
    public static bool IsDue(Watch w, DateTime utcNow, TimeZoneInfo zone, int hour)
    {
        if (!w.Enabled) return false;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone);
        if (now.Hour < hour) return false;
        return w.LastRunAt is not { } last || TimeZoneInfo.ConvertTimeFromUtc(last, zone).Date < now.Date;
    }

    public static TimeZoneInfo Zone(IConfiguration config)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(config["Watch:TimeZone"] ?? "Europe/Stockholm"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
    }
}

// Kollar regelbundet om någon bevakning ska köras och lägger den i kön.
public class WatchScheduler(IServiceScopeFactory scopes, RunQueue queue, ILlm llm, IConfiguration config, ILogger<WatchScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(config.GetValue("Watch:CheckMinutes", 15));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await RunDueAsync(DateTime.UtcNow, ct);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(e, "Kunde inte köra bevakningarna.");
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task<int> RunDueAsync(DateTime utcNow, CancellationToken ct)
    {
        if (!llm.Enabled) return 0;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobbAgentDbContext>();
        if (!await db.Profiles.AnyAsync(ct)) return 0;

        var zone = Watches.Zone(config);
        var hour = config.GetValue("Watch:Hour", 7);
        var due = (await db.Watches.Where(w => w.Enabled).ToListAsync(ct)).Where(w => Watches.IsDue(w, utcNow, zone, hour)).ToList();
        // Varje bevakning körs på det språk du hade när du skapade den.
        var runs = due.Select(w =>
        {
            Language.Current = Language.Parse(w.UiLanguage);
            return Watches.CreateRun(w);
        }).ToList();
        db.Runs.AddRange(runs);
        due.ForEach(w => w.LastRunAt = utcNow);
        await db.SaveChangesAsync(ct);
        runs.ForEach(r => queue.Enqueue(r.Id));
        if (due.Count > 0) logger.LogInformation("Startade {Count} bevakningar.", due.Count);
        return due.Count;
    }
}
