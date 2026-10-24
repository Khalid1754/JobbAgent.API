using System.Net;
using System.Net.Http.Json;
using System.Text;
using JobbAgent.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using JobbAgent.Api.Services;

namespace JobbAgent.Api.Tests;

public class CvTests : IDisposable
{
    private const string OtherCv = "Khalid Hassan. Lagerarbetare med truckkort, fem år på PostNord och ansvar för inleveranser.";
    private readonly TestApp _app = new();
    private readonly HttpClient _client;

    public CvTests() => _client = _app.CreateClient();
    public void Dispose() => _app.Dispose();

    private async Task<List<CvDto>> ListAsync() => (await _client.GetFromJsonAsync<List<CvDto>>("/api/cvs", TestApp.Json))!;

    [Fact]
    public async Task Versions_CanBeCreatedRenamedAndMadeDefault()
    {
        var dev = await TestApp.SaveCvAsync(_client, OrchestratorTests.Cv, "Utvecklare");
        var other = await TestApp.SaveCvAsync(_client, OtherCv, "Lager");
        Assert.True(dev.IsDefault);
        Assert.False(other.IsDefault);

        var renamed = await _client.PutAsJsonAsync($"/api/cvs/{other.Id}", new { cvText = OtherCv, name = "Lager och logistik", email = "k@example.se" });
        Assert.Equal(("Lager och logistik", "k@example.se"), ((await renamed.Content.ReadFromJsonAsync<CvDto>(TestApp.Json))!.Name, (await ListAsync()).Last().Email));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsync($"/api/cvs/{other.Id}/default", null)).StatusCode);
        var list = await ListAsync();
        Assert.Equal([other.Id, dev.Id], list.Select(c => c.Id));
        Assert.True(list[0].IsDefault);
    }

    [Fact]
    public async Task Delete_KeepsAtLeastOneAndMovesDefault()
    {
        var dev = await TestApp.SaveCvAsync(_client, OrchestratorTests.Cv, "Utvecklare");
        var other = await TestApp.SaveCvAsync(_client, OtherCv, "Lager");
        var watch = await (await _client.PostAsJsonAsync("/api/watches", new { keywords = "lager", cvId = other.Id })).Content.ReadFromJsonAsync<WatchDto>(TestApp.Json);
        Assert.Equal(other.Id, watch!.CvId);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/cvs/{dev.Id}")).StatusCode);
        Assert.True(Assert.Single(await ListAsync()).IsDefault);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.DeleteAsync($"/api/cvs/{other.Id}")).StatusCode);

        await TestApp.SaveCvAsync(_client, OrchestratorTests.Cv, "Ny");
        await _client.DeleteAsync($"/api/cvs/{other.Id}");
        Assert.Null((await _client.GetFromJsonAsync<List<WatchDto>>("/api/watches", TestApp.Json))!.Single().CvId);
    }

    [Fact]
    public async Task Upload_CreatesNewVersionWithSameContact()
    {
        await _client.PostAsJsonAsync("/api/cvs", new { cvText = OrchestratorTests.Cv, fullName = "Khalid Hassan", phone = "070-123 45 67" });
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(OtherCv)), "file", "Lager-CV.txt");
        var response = await _client.PostAsync("/api/cvs/upload", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var cv = (await response.Content.ReadFromJsonAsync<CvDto>(TestApp.Json))!;
        Assert.Equal(("Lager-CV", "Lager-CV.txt", "070-123 45 67", false), (cv.Name, cv.FileName, cv.Phone, cv.IsDefault));
    }

    [Fact]
    public async Task Upload_ReplacesTextOfExistingVersion()
    {
        var cv = await TestApp.SaveCvAsync(_client, OrchestratorTests.Cv);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(OtherCv)), "file", "nytt.txt");
        var updated = (await (await _client.PostAsync($"/api/cvs/{cv.Id}/upload", form)).Content.ReadFromJsonAsync<CvDto>(TestApp.Json))!;
        Assert.Equal((cv.Id, OtherCv, "nytt.txt"), (updated.Id, updated.CvText, updated.FileName));
    }

    [Fact]
    public async Task Runs_UseTheChosenCv()
    {
        await TestApp.SaveCvAsync(_client, OrchestratorTests.Cv, "Utvecklare");
        var other = await TestApp.SaveCvAsync(_client, OtherCv, "Lager");

        var letter = await TestApp.RunToEndAsync(_client, await _client.PostAsJsonAsync("/api/letters", new { adText = TestApp.AdText, cvId = other.Id }));
        Assert.Equal(other.Id, letter.CvId);
        var llm = (FakeLlm)_app.Services.GetRequiredService<ILlm>();
        Assert.Contains(llm.Prompts, p => p.Contains("Lagerarbetare"));
        Assert.DoesNotContain(llm.Prompts, p => p.Contains("Systemutvecklare med tre år"));

        var bad = await _client.PostAsJsonAsync("/api/searches", new { cvId = 999 });
        Assert.Contains("finns inte", await bad.Content.ReadAsStringAsync());
    }
}
