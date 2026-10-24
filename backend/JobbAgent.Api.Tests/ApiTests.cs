using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobbAgent.Api.Data;
using JobbAgent.Api.Models;
using JobbAgent.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobbAgent.Api.Tests;

public class ApiTests : IDisposable
{
    // Hela API:t med minnesdatabas, påhittad AI och påhittad Platsbanken. En ny databas per test.
    private class App : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<JobbAgentDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<JobbAgentDbContext>));
            services.AddDbContext<JobbAgentDbContext>(o => o.UseInMemoryDatabase(_dbName));
            services.RemoveAll<ILlm>();
            services.AddSingleton<ILlm>(new FakeLlm());
            services.RemoveAll<IJobBoard>();
            services.AddSingleton<IJobBoard>(new FakeJobBoard());
        });
    }

    private readonly App _app = new();
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions Json = ApiJson.Options;
    private const string AdText = "Vi söker en backendutvecklare till Acme AB i Stockholm. Du kan C# och .NET och gillar att bygga API:er tillsammans med andra.";

    public ApiTests() => _client = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    private Task SaveCvAsync() => _client.PutAsJsonAsync("/api/profile", new { cvText = OrchestratorTests.Cv });

    private async Task<RunDto> RunToEndAsync(HttpResponseMessage created)
    {
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = (await created.Content.ReadFromJsonAsync<RunSummaryDto>(Json))!;
        // Händelseströmmen tar slut när körningen är klar.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var stream = await _client.GetStringAsync($"/api/runs/{run.Id}/events", cts.Token);
        Assert.Contains("\"status\":\"completed\"", stream);
        return (await _client.GetFromJsonAsync<RunDto>($"/api/runs/{run.Id}", Json))!;
    }

    [Fact]
    public async Task Profile_IsSavedAndValidated()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync("/api/profile", new { cvText = "för kort" })).StatusCode);

        await SaveCvAsync();
        var profile = (await _client.GetFromJsonAsync<ProfileDto>("/api/profile", Json))!;
        Assert.Equal(OrchestratorTests.Cv, profile.CvText);
        var status = await _client.GetFromJsonAsync<JsonElement>("/api/status");
        Assert.True(status.GetProperty("hasCv").GetBoolean());
    }

    [Fact]
    public async Task UploadedCv_IsRead()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(OrchestratorTests.Cv)), "file", "mitt-cv.txt");
        var response = await _client.PostAsync("/api/profile/upload", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = (await response.Content.ReadFromJsonAsync<ProfileDto>(Json))!;
        Assert.Equal(("mitt-cv.txt", OrchestratorTests.Cv), (profile.FileName, profile.CvText));
    }

    [Fact]
    public async Task WithoutCv_RunsAreRejected()
    {
        var response = await _client.PostAsJsonAsync("/api/searches", new { location = "Stockholm" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CV", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_ReturnsRankedHits()
    {
        await SaveCvAsync();
        var run = await RunToEndAsync(await _client.PostAsJsonAsync("/api/searches", new { location = "Stockholm", remoteOnly = true }));

        Assert.Equal(RunKind.Search, run.Kind);
        Assert.Equal("Jobb som passar mitt CV i Stockholm (distans)", run.Title);
        Assert.Equal([90, 40], run.Hits!.Select(h => h.Score));
    }

    [Fact]
    public async Task Letter_FromPastedText_CanBeEditedAndDeleted()
    {
        await SaveCvAsync();
        var run = await RunToEndAsync(await _client.PostAsJsonAsync("/api/letters", new { adText = AdText }));

        Assert.Equal("Hej Anna! Brev v1", run.Letter);
        Assert.Equal(82, run.Match!.Score);
        Assert.Equal("Acme AB", run.Requirements!.Company);

        var edit = await _client.PutAsJsonAsync($"/api/runs/{run.Id}/letter", new { letter = "Mitt eget brev" });
        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        Assert.Equal("Mitt eget brev", (await _client.GetFromJsonAsync<RunDto>($"/api/runs/{run.Id}", Json))!.Letter);

        var list = (await _client.GetFromJsonAsync<List<RunSummaryDto>>("/api/runs", Json))!;
        Assert.Equal(82, list.Single(r => r.Id == run.Id).MatchScore);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/runs/{run.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/runs/{run.Id}")).StatusCode);
    }

    [Fact]
    public async Task Letter_FromPlatsbankenLink_FetchesAd()
    {
        await SaveCvAsync();
        var run = await RunToEndAsync(await _client.PostAsJsonAsync("/api/letters",
            new { adUrl = "https://arbetsformedlingen.se/platsbanken/annonser/777", language = "en" }));

        Assert.Equal("https://arbetsformedlingen.se/platsbanken/annonser/777", run.AdUrl);
        Assert.Contains("Vi söker en Backendutvecklare", run.AdText);
        Assert.Equal("en", run.Language);
    }

    [Theory]
    [InlineData("""{"adText":"för kort"}""", "för kort")]
    [InlineData("""{"platsbankenId":"404"}""", "finns inte längre")]
    [InlineData("""{"adText":"x","language":"de"}""", "Språket")]
    public async Task Letter_BadInput_IsRejected(string body, string expected)
    {
        await SaveCvAsync();
        var response = await _client.PostAsync("/api/letters", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }
}
