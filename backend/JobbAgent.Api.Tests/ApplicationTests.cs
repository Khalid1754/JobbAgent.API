using System.Net;
using System.Net.Http.Json;
using JobbAgent.Api.Models;

namespace JobbAgent.Api.Tests;

public class ApplicationTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public ApplicationTests() => _client = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    private async Task<ApplicationDto> CreateAsync(object body, HttpStatusCode expected = HttpStatusCode.Created)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", body);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApplicationDto>(TestApp.Json))!;
    }

    [Fact]
    public async Task SavedJob_MovesThroughStages()
    {
        var saved = await CreateAsync(new { title = "Backendutvecklare", company = "Acme AB", platsbankenId = "1", matchScore = 88 });
        Assert.Equal(Stage.Saved, saved.Stage);
        Assert.Null(saved.AppliedAt);

        var update = new UpdateApplicationRequest(saved.Title, saved.Company, null, null, null, Stage.Applied, "Skickade via mejl", null, null);
        var applied = (await (await _client.PutAsJsonAsync($"/api/applications/{saved.Id}", update))
            .Content.ReadFromJsonAsync<ApplicationDto>(TestApp.Json))!;
        Assert.Equal(Stage.Applied, applied.Stage);
        Assert.NotNull(applied.AppliedAt);
        Assert.Equal("Skickade via mejl", applied.Notes);

        var interview = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        var moved = (await (await _client.PutAsJsonAsync($"/api/applications/{saved.Id}",
                update with { Stage = Stage.Interview, AppliedAt = applied.AppliedAt, InterviewAt = interview }))
            .Content.ReadFromJsonAsync<ApplicationDto>(TestApp.Json))!;
        Assert.Equal((Stage.Interview, interview, applied.AppliedAt), (moved.Stage, moved.InterviewAt, moved.AppliedAt));

        var list = (await _client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications", TestApp.Json))!;
        Assert.Single(list);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/applications/{saved.Id}")).StatusCode);
        Assert.Empty((await _client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications", TestApp.Json))!);
    }

    [Fact]
    public async Task SameAd_IsOnlyAddedOnce()
    {
        var first = await CreateAsync(new { title = "Utvecklare", platsbankenId = "42" });
        var second = await CreateAsync(new { title = "Utvecklare", platsbankenId = "42" }, HttpStatusCode.OK);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task FromLetter_CopiesJobDetails()
    {
        await _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });
        var letter = await _app.RunLetterAsync(_client);

        var application = await CreateAsync(new { letterRunId = letter.Id, stage = "applied" });
        Assert.Equal(("Backendutvecklare", "Acme AB", 82, Stage.Applied), (application.Title, application.Company, application.MatchScore, application.Stage));
        Assert.Equal(letter.Id, application.LetterRunId);

        // Tas brevet bort ligger jobbet kvar på tavlan.
        await _client.DeleteAsync($"/api/runs/{letter.Id}");
        var list = (await _client.GetFromJsonAsync<List<ApplicationDto>>("/api/applications", TestApp.Json))!;
        Assert.Null(Assert.Single(list).LetterRunId);
    }

    [Fact]
    public async Task LetterToSavedAd_IsLinkedToSameCard()
    {
        await _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });
        var saved = await CreateAsync(new { title = "Backendutvecklare", platsbankenId = "777", matchScore = 60 });
        var letter = await TestApp.RunToEndAsync(_client, await _client.PostAsJsonAsync("/api/letters", new { platsbankenId = "777" }));

        var linked = await CreateAsync(new { letterRunId = letter.Id, stage = "applied" }, HttpStatusCode.OK);
        Assert.Equal((saved.Id, letter.Id, Stage.Applied, 82), (linked.Id, linked.LetterRunId, linked.Stage, linked.MatchScore));
    }

    [Fact]
    public async Task Interview_CanStartFromLetterOrBoard()
    {
        await _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });
        var letter = await _app.RunLetterAsync(_client);
        var fromLetter = await TestApp.RunToEndAsync(_client, await _client.PostAsJsonAsync("/api/interviews", new { letterRunId = letter.Id }));
        Assert.Equal((RunKind.Interview, "Intervju: Backendutvecklare", letter.Id), (fromLetter.Kind, fromLetter.Title, fromLetter.SourceRunId));
        Assert.NotNull(fromLetter.Interview);

        var card = await CreateAsync(new { title = "Systemutvecklare", platsbankenId = "55" });
        var fromBoard = await TestApp.RunToEndAsync(_client, await _client.PostAsJsonAsync("/api/interviews", new { applicationId = card.Id }));
        Assert.Equal(card.Id, fromBoard.ApplicationId);
        Assert.Contains("Backendutvecklare", fromBoard.AdText);

        var noLink = await CreateAsync(new { title = "Okänt jobb" });
        var response = await _client.PostAsJsonAsync("/api/interviews", new { applicationId = noLink.Id });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("länk", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("""{"title":""}""", "titel")]
    [InlineData("""{"letterRunId":"00000000-0000-0000-0000-000000000001"}""", "Brevet finns inte")]
    public async Task BadInput_IsRejected(string body, string expected)
    {
        var response = await _client.PostAsync("/api/applications", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }
}
