using System.Net;
using JobbAgent.Api.Services;
using Microsoft.Extensions.Configuration;

namespace JobbAgent.Api.Tests;

public class PlatsbankenTests
{
    // Förkortat svar från https://jobsearch.api.jobtechdev.se/search
    private const string SearchResponse = """
        {"total":{"value":1},"hits":[{
          "id":"29812345","headline":"Backendutvecklare .NET",
          "webpage_url":"https://arbetsformedlingen.se/platsbanken/annonser/29812345",
          "application_deadline":"2026-11-30T23:59:59","publication_date":"2026-10-20T08:00:00",
          "employer":{"name":"Acme AB"},"occupation":{"label":"Mjukvaru- och systemutvecklare m.fl."},
          "workplace_address":{"municipality":"Stockholm","region":"Stockholms län"},
          "description":{"text":"Vi söker en utvecklare."},"remote_work":true
        },{"id":"1","headline":null}]}
        """;

    private static PlatsbankenClient Client(FakeHandler handler) =>
        new(new HttpClient(handler), new ConfigurationBuilder().Build());

    [Fact]
    public async Task Search_ParsesHitsAndSkipsBrokenOnes()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, SearchResponse);
        var ads = await Client(handler).SearchAsync("utvecklare stockholm", remoteOnly: true, limit: 10, CancellationToken.None);

        var ad = Assert.Single(ads);
        Assert.Equal(("29812345", "Backendutvecklare .NET", "Acme AB", "Stockholm"), (ad.Id, ad.Title, ad.Employer, ad.Municipality));
        Assert.Equal("Vi söker en utvecklare.", ad.Description);
        Assert.Equal(new DateTime(2026, 11, 30, 23, 59, 59), ad.Deadline);
        Assert.True(ad.Remote);
        Assert.Equal("https://jobsearch.api.jobtechdev.se/search?q=utvecklare%20stockholm&limit=10&remote=true",
            handler.Request!.RequestUri!.AbsoluteUri);
        Assert.Contains("application/json", handler.Request.Headers.Accept.ToString());
    }

    [Fact]
    public async Task Get_MissingAd_ReturnsNull()
    {
        var handler = new FakeHandler(HttpStatusCode.NotFound, "{}");
        Assert.Null(await Client(handler).GetAsync("123", CancellationToken.None));
        Assert.Equal("/ad/123", handler.Request!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ServerError_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable, "");
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(handler).SearchAsync("x", false, 5, CancellationToken.None));
    }
}
