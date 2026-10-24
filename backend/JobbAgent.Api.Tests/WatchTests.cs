using System.Net;
using System.Net.Http.Json;
using JobbAgent.Api.Agents;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobbAgent.Api.Tests;

public class WatchTests : IDisposable
{
    private static readonly TimeZoneInfo Stockholm = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public WatchTests() => _client = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    [Theory]
    // 2026-10-24 är sommartid, Stockholm = UTC+2.
    [InlineData("2026-10-24T04:30:00Z", null, false)]                    // 06:30, för tidigt
    [InlineData("2026-10-24T05:00:00Z", null, true)]                     // 07:00, aldrig körd
    [InlineData("2026-10-24T09:00:00Z", "2026-10-24T05:10:00Z", false)]  // redan körd i dag
    [InlineData("2026-10-24T09:00:00Z", "2026-10-23T20:00:00Z", true)]   // senast i går
    [InlineData("2026-10-24T09:00:00Z", "2026-10-23T22:30:00Z", false)]  // 00:30 i dag lokal tid
    public void IsDue_OncePerDayFromSevenLocalTime(string now, string? lastRun, bool expected)
    {
        var watch = new Watch { LastRunAt = lastRun is null ? null : DateTime.Parse(lastRun).ToUniversalTime() };
        Assert.Equal(expected, Watches.IsDue(watch, DateTime.Parse(now).ToUniversalTime(), Stockholm, 7));
        Assert.False(Watches.IsDue(new Watch { Enabled = false }, DateTime.Parse(now).ToUniversalTime(), Stockholm, 0));
    }

    [Fact]
    public void MarkNew_CountsOnlyUnseenGoodMatches()
    {
        var watch = new Watch { MinScore = 70, SeenAdIdsJson = """["1"]""" };
        var hits = new List<SearchHit>
        {
            new("1", "Gammal", null, null, "u", null, 95, "r"),
            new("2", "Ny bra", null, null, "u", null, 80, "r"),
            new("3", "Ny svag", null, null, "u", null, 30, "r"),
        };
        var marked = Watches.MarkNew(watch, hits, Guid.NewGuid());

        Assert.Equal([false, true, true], marked.Select(h => h.IsNew));
        Assert.Equal(1, watch.NewMatches);
        Assert.Equal("""["1","2","3"]""", watch.SeenAdIdsJson);
    }

    [Fact]
    public async Task RunNow_MarksNewAdsAndRemembersThem()
    {
        await _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });
        var created = await _client.PostAsJsonAsync("/api/watches", new { location = "Stockholm", remoteOnly = false, minScore = 70 });
        var watch = (await created.Content.ReadFromJsonAsync<WatchDto>(TestApp.Json))!;
        Assert.Equal("Jobb som passar mitt CV i Stockholm", watch.Title);

        var first = await TestApp.RunToEndAsync(_client, await _client.PostAsync($"/api/watches/{watch.Id}/run", null));
        Assert.Equal(RunKind.Search, first.Kind);
        Assert.All(first.Hits!, h => Assert.True(h.IsNew));
        var afterFirst = (await _client.GetFromJsonAsync<List<WatchDto>>("/api/watches", TestApp.Json))!.Single();
        Assert.Equal((1, first.Id), (afterFirst.NewMatches, afterFirst.LastRunId));

        var second = await TestApp.RunToEndAsync(_client, await _client.PostAsync($"/api/watches/{watch.Id}/run", null));
        Assert.All(second.Hits!, h => Assert.False(h.IsNew));
        Assert.Equal(0, (await _client.GetFromJsonAsync<List<WatchDto>>("/api/watches", TestApp.Json))!.Single().NewMatches);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/watches/{watch.Id}")).StatusCode);
    }

    [Fact]
    public async Task Scheduler_StartsDueWatches()
    {
        await _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });
        await _client.PostAsJsonAsync("/api/watches", new { keywords = "backend" });
        await _client.PostAsJsonAsync("/api/watches", new { keywords = "avstängd", enabled = false });
        var scheduler = _app.Services.GetRequiredService<WatchScheduler>();

        var morning = new DateTime(2026, 10, 24, 6, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, await scheduler.RunDueAsync(morning, CancellationToken.None));
        Assert.Equal(0, await scheduler.RunDueAsync(morning.AddHours(3), CancellationToken.None));

        var runs = (await _client.GetFromJsonAsync<List<RunSummaryDto>>("/api/runs", TestApp.Json))!;
        Assert.Equal("Bevakning: backend", Assert.Single(runs).Title);
    }
}
