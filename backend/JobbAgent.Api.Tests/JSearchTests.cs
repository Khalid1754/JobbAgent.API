using System.Net;
using JobbAgent.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobbAgent.Api.Tests;

public class JSearchTests
{
    // Förkortat svar från https://jsearch.p.rapidapi.com/search
    private const string SearchResponse = """
        {"status":"OK","data":[{
          "job_id":"abc123==","job_title":"Fullstackutvecklare","employer_name":"Nordbyte","job_publisher":"LinkedIn",
          "job_apply_link":"https://www.linkedin.com/jobs/view/123","job_description":"Vi söker en fullstackutvecklare.",
          "job_city":"Solna","job_is_remote":false,"job_posted_at_datetime_utc":"2026-10-20T08:00:00.000Z",
          "job_offer_expiration_datetime_utc":"2026-11-20T00:00:00.000Z"
        },{"job_id":"x","job_title":null}]}
        """;

    private static JSearchClient Client(FakeHandler handler, string? key = "test-key") =>
        new(new HttpClient(handler), new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JSearch:ApiKey"] = key }).Build());

    [Fact]
    public async Task Search_ParsesJobsWithSourceAndSendsKey()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SearchResponse);
        var ads = await Client(handler).SearchAsync("utvecklare stockholm", remoteOnly: true, limit: 10, CancellationToken.None);

        var ad = Assert.Single(ads);
        Assert.Equal(("js:abc123==", "Fullstackutvecklare", "Nordbyte", "Solna", "LinkedIn"), (ad.Id, ad.Title, ad.Employer, ad.Municipality, ad.Source));
        Assert.Equal("https://www.linkedin.com/jobs/view/123", ad.Url);
        Assert.Equal(new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc), ad.Deadline);
        Assert.Equal("https://jsearch.p.rapidapi.com/search?query=utvecklare%20stockholm&page=1&num_pages=1&country=se&date_posted=month&work_from_home=true",
            handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Equal("test-key", handler.Request.Headers.GetValues("X-RapidAPI-Key").Single());
    }

    [Fact]
    public async Task Get_UsesJobIdWithoutPrefix()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SearchResponse);
        var ad = await Client(handler).GetAsync("js:abc123==", CancellationToken.None);

        Assert.Equal("Fullstackutvecklare", ad!.Title);
        Assert.Equal("https://jsearch.p.rapidapi.com/job-details?job_id=abc123%3D%3D&country=se", handler.Request!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void WithoutKey_IsDisabled() =>
        Assert.Equal(Environment.GetEnvironmentVariable("RAPIDAPI_KEY") is not null, Client(new FakeHandler(HttpStatusCode.OK, "{}"), key: null).Enabled);

    private static JobBoards Boards(FakeHandler platsbanken, FakeHandler jsearch) =>
        new(new PlatsbankenClient(new HttpClient(platsbanken), new ConfigurationBuilder().Build()), Client(jsearch), NullLogger<JobBoards>.Instance);

    private const string PlatsbankenResponse = """
        {"hits":[
          {"id":"1","headline":"Fullstackutvecklare","employer":{"name":"Nordbyte AB"},"description":{"text":"..."}},
          {"id":"2","headline":"Backendutvecklare","employer":{"name":"Acme AB"},"description":{"text":"..."}}
        ]}
        """;

    [Fact]
    public async Task Boards_MergeSourcesAndRemoveDuplicates()
    {
        var both = Boards(new FakeHandler(HttpStatusCode.OK, PlatsbankenResponse),
            new FakeHandler(HttpStatusCode.OK, SearchResponse.Replace("\"Nordbyte\"", "\"Nordbyte AB\"")));
        var ads = await both.SearchAsync("utvecklare", false, 10, CancellationToken.None);

        // Samma jobb hos Nordbyte AB finns i båda källorna och visas bara en gång.
        Assert.Equal(["1", "2"], ads.Select(a => a.Id));

        var different = Boards(new FakeHandler(HttpStatusCode.OK, PlatsbankenResponse), new FakeHandler(HttpStatusCode.OK, SearchResponse));
        Assert.Equal(["1", "js:abc123==", "2"], (await different.SearchAsync("utvecklare", false, 10, CancellationToken.None)).Select(a => a.Id));
    }

    [Fact]
    public async Task Boards_KeepGoingWhenOneSourceFails()
    {
        var quotaUsedUp = Boards(new FakeHandler(HttpStatusCode.OK, PlatsbankenResponse), new FakeHandler(HttpStatusCode.TooManyRequests, "{}"));
        Assert.Equal(2, (await quotaUsedUp.SearchAsync("utvecklare", false, 10, CancellationToken.None)).Count);

        var bothDown = Boards(new FakeHandler(HttpStatusCode.BadGateway, "{}"), new FakeHandler(HttpStatusCode.TooManyRequests, "{}"));
        await Assert.ThrowsAsync<HttpRequestException>(() => bothDown.SearchAsync("utvecklare", false, 10, CancellationToken.None));
    }

    [Fact]
    public async Task Boards_RouteGetBySource()
    {
        var platsbanken = new FakeHandler(HttpStatusCode.OK, """{"id":"9","headline":"Testare"}""");
        var jsearch = new FakeHandler(HttpStatusCode.OK, SearchResponse);
        var boards = Boards(platsbanken, jsearch);

        Assert.Equal("Fullstackutvecklare", (await boards.GetAsync("js:abc123==", CancellationToken.None))!.Title);
        Assert.Equal("Testare", (await boards.GetAsync("9", CancellationToken.None))!.Title);
        Assert.Equal((1, 1), (platsbanken.Calls, jsearch.Calls));
    }
}
