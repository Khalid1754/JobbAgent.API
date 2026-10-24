using System.Net;
using System.Net.Http.Json;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace JobbAgent.Api.Tests;

// Engelska i gränssnittet: felmeddelanden, agenternas steg och analyser följer Accept-Language.
public class LanguageTests : IDisposable
{
    private readonly TestApp _app = new();
    public void Dispose() => _app.Dispose();

    private HttpClient Client(string lang)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(lang);
        return client;
    }

    [Theory]
    [InlineData("en", Lang.En)]
    [InlineData("en-GB,en;q=0.9", Lang.En)]
    [InlineData("sv", Lang.Sv)]
    [InlineData(null, Lang.Sv)]
    public void Parse_DefaultsToSwedish(string? header, Lang expected) => Assert.Equal(expected, Language.Parse(header));

    [Fact]
    public async Task Errors_FollowAcceptLanguage()
    {
        var english = await Client("en").PostAsJsonAsync("/api/searches", new { });
        var swedish = await Client("sv").PostAsJsonAsync("/api/searches", new { });
        Assert.Equal(HttpStatusCode.BadRequest, english.StatusCode);
        Assert.Contains("Add your CV first.", await english.Content.ReadAsStringAsync());
        Assert.Contains("Lägg in ditt CV först.", await swedish.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Run_InEnglish_WritesStepsAndAsksAgentsForEnglish()
    {
        var client = Client("en");
        await TestApp.SaveCvAsync(client, OrchestratorTests.Cv);
        var run = await TestApp.RunToEndAsync(client, await client.PostAsJsonAsync("/api/letters", new { adText = TestApp.AdText }));

        Assert.Contains(run.Steps, s => s.Summary == "Reading the ad");
        Assert.Contains(run.Steps, s => s.Summary == "The letter is approved");
        Assert.DoesNotContain(run.Steps, s => s.Summary == "Läser annonsen");
        var llm = (FakeLlm)_app.Services.GetRequiredService<ILlm>();
        Assert.Contains(llm.Prompts, p => p.Contains("CV tips in English"));
    }

    [Fact]
    public async Task Run_InSwedish_StaysSwedish()
    {
        var client = Client("sv");
        await TestApp.SaveCvAsync(client, OrchestratorTests.Cv);
        var run = await TestApp.RunToEndAsync(client, await client.PostAsJsonAsync("/api/letters", new { adText = TestApp.AdText }));

        Assert.Contains(run.Steps, s => s.Summary == "Läser annonsen");
        var llm = (FakeLlm)_app.Services.GetRequiredService<ILlm>();
        Assert.Contains(llm.Prompts, p => p.Contains("CV tips in Swedish"));
    }

    [Fact]
    public async Task Watch_RemembersLanguageForMorningRuns()
    {
        var client = Client("en");
        await TestApp.SaveCvAsync(client, OrchestratorTests.Cv);
        var watch = (await (await client.PostAsJsonAsync("/api/watches", new { location = "Malmö", remoteOnly = true }))
            .Content.ReadFromJsonAsync<WatchDto>(TestApp.Json))!;
        Assert.Equal("Jobs that fit my CV in Malmö (remote)", watch.Title);

        var scheduler = _app.Services.GetRequiredService<WatchScheduler>();
        Language.Current = Lang.Sv;
        Assert.Equal(1, await scheduler.RunDueAsync(new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc), CancellationToken.None));
        var runs = await Client("sv").GetFromJsonAsync<List<RunSummaryDto>>("/api/runs", TestApp.Json);
        Assert.Equal("Saved search: Jobs that fit my CV in Malmö (remote)", runs!.Single(r => r.WatchId == watch.Id).Title);
    }
}
