using JobbAgent.Api.Agents;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobbAgent.Api.Tests;

public class OrchestratorTests
{
    public const string Cv = "Khalid Hassan. Systemutvecklare med tre år C# och .NET, React och SQL Server.";

    private readonly FakeLlm _llm = new();
    private readonly FakeJobBoard _board = new();
    private readonly ServiceProvider _services;

    public OrchestratorTests()
    {
        var dbName = Guid.NewGuid().ToString();
        _services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddDbContext<JobbAgentDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddSingleton<ILlm>(_llm)
            .AddSingleton<IJobBoard>(_board)
            .AddSingleton<Searcher>().AddSingleton<Interpreter>().AddSingleton<Matcher>()
            .AddSingleton<Writer>().AddSingleton<Reviewer>().AddSingleton<Coach>()
            .AddSingleton<RunEvents>().AddSingleton<RunOrchestrator>()
            .BuildServiceProvider();
    }

    private async Task<Run> ExecuteAsync(Run run, bool withCv = true)
    {
        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobbAgentDbContext>();
            if (withCv) db.Profiles.Add(new Profile { CvText = Cv });
            db.Runs.Add(run);
            await db.SaveChangesAsync();
        }
        await _services.GetRequiredService<RunOrchestrator>().ExecuteAsync(run.Id, CancellationToken.None);

        using var readScope = _services.CreateScope();
        return await readScope.ServiceProvider.GetRequiredService<JobbAgentDbContext>().Runs
            .Include(r => r.Steps).SingleAsync(r => r.Id == run.Id);
    }

    private static Run LetterRun(string language = "auto") =>
        new() { Kind = RunKind.Letter, Title = "Ny ansökan", AdText = "Vi söker en backendutvecklare som kan C#.", Language = language };

    private static string[] Handoffs(Run run) => run.Steps.OrderBy(s => s.Sequence)
        .Where(s => s.Kind == StepKind.Handoff).Select(s => $"{s.Agent}->{s.ToAgent}").ToArray();

    [Fact]
    public async Task Search_RanksAdsBestFirst()
    {
        var run = await ExecuteAsync(new Run { Kind = RunKind.Search, Title = "Sök", Location = "Stockholm", RemoteOnly = true });

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["Sökare->Matchare"], Handoffs(run));
        Assert.Equal(["systemutvecklare Stockholm", "backendutvecklare Stockholm"], _board.Searches.Select(s => s.Query));
        Assert.All(_board.Searches, s => Assert.True(s.Remote));
        var hits = RunDto.From(run).Hits!;
        Assert.Equal(["2", "1"], hits.Select(h => h.Id));
        Assert.Equal(90, run.MatchScore);
    }

    [Fact]
    public async Task Search_WhenJobSitesAreDown_Fails()
    {
        _board.Fail = true;
        var run = await ExecuteAsync(new Run { Kind = RunKind.Search, Title = "Sök" });

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Contains("jobbsajterna", run.Error);
        Assert.DoesNotContain("Matchare", _llm.Calls);
    }

    [Fact]
    public async Task Letter_GoesThroughAllAgents()
    {
        var run = await ExecuteAsync(LetterRun());

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["Tolkare", "Matchare", "Skribent", "Granskare"], _llm.Calls);
        Assert.Equal(["Tolkare->Matchare", "Matchare->Skribent", "Skribent->Granskare"], Handoffs(run));
        Assert.Equal("Hej Anna! Brev v1", run.Letter);
        Assert.Equal("Backendutvecklare", run.Title);
        Assert.Equal("Acme AB", run.Company);
        Assert.Equal("sv", run.Language);
        Assert.Equal(82, run.MatchScore);
        Assert.Contains(Cv, _llm.Prompts[1]);
        var tip = Assert.Single(RunDto.From(run).Match!.CvTips);
        Assert.Equal("Profil", tip.Section);
        Assert.Contains("Anpassa CV:t", run.Steps.Single(s => s.Agent == "Matchare" && s.Kind == StepKind.Completed).Detail);
    }

    [Fact]
    public void OldMatchWithoutCvTips_CanStillBeShown()
    {
        var old = new Run { MatchJson = """{"score":70,"summary":"Ok","matches":[],"gaps":[],"highlights":[]}""" };
        var match = RunDto.From(old).Match!;
        Assert.Contains("70 %", Matcher.Format(match));
    }

    [Fact]
    public async Task Letter_ChosenLanguageIsKept()
    {
        var run = await ExecuteAsync(LetterRun("en"));
        Assert.Equal("en", run.Language);
        Assert.Contains("Write the letter in English", _llm.Prompts[2]);
    }

    [Fact]
    public async Task Letter_RejectedOnce_IsRewritten()
    {
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Ta bort påhittad Azure-erfarenhet."}""");
        var run = await ExecuteAsync(LetterRun());

        Assert.Equal("Hej Anna! Brev v2", run.Letter);
        Assert.Contains("Granskare->Skribent", Handoffs(run));
        Assert.Equal(2, _llm.Calls.Count(c => c == "Skribent"));
    }

    [Fact]
    public async Task Letter_RejectedTwice_StopsAfterMaxRevisions()
    {
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Fel 1"}""");
        _llm.ReviewAnswers.Enqueue("""{"approved":false,"feedback":"Fel 2"}""");
        var run = await ExecuteAsync(LetterRun());

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(1 + RunOrchestrator.MaxRevisions, _llm.Calls.Count(c => c == "Skribent"));
    }

    [Fact]
    public async Task AgentError_IsShownAndRunFails()
    {
        _llm.FailFor = "Matchare";
        var run = await ExecuteAsync(LetterRun());

        Assert.Equal(RunStatus.Failed, run.Status);
        Assert.Equal("Matchare gick sönder", run.Error);
        var last = run.Steps.OrderBy(s => s.Sequence).Last();
        Assert.Equal(("Matchare", StepKind.Failed), (last.Agent, last.Kind));
    }

    [Fact]
    public async Task Interview_FromAd_ReadsAndMatchesFirst()
    {
        var run = await ExecuteAsync(new Run { Kind = RunKind.Interview, Title = "Intervju: Ny", AdText = "Vi söker en backendutvecklare.", Language = "auto" });

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["Tolkare", "Matchare", "Intervjucoach"], _llm.Calls);
        Assert.Equal(["Tolkare->Matchare", "Matchare->Intervjucoach"], Handoffs(run));
        Assert.Equal("Intervju: Backendutvecklare", run.Title);
        var prep = RunDto.From(run).Interview!;
        Assert.Equal("Berätta om ett API du byggt.", Assert.Single(prep.Questions).Question);
    }

    [Fact]
    public async Task Interview_FromLetter_ReusesRequirementsAndMatch()
    {
        var letter = await ExecuteAsync(LetterRun());
        _llm.Calls.Clear();
        var run = await ExecuteAsync(new Run
        {
            Kind = RunKind.Interview, Title = "Intervju: Backendutvecklare", Language = "en",
            RequirementsJson = letter.RequirementsJson, MatchJson = letter.MatchJson,
        }, withCv: false);

        Assert.Equal(["Intervjucoach"], _llm.Calls);
        Assert.Contains("Write everything in English", _llm.Prompts.Last());
        Assert.Contains(run.Steps, s => s.Summary == "Använder kraven från brevet");
    }

    [Fact]
    public async Task WithoutCv_Fails()
    {
        var run = await ExecuteAsync(LetterRun(), withCv: false);
        Assert.Equal("Lägg in ditt CV först.", run.Error);
        Assert.Empty(_llm.Calls);
    }
}
