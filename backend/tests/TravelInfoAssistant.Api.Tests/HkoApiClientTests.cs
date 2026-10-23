using System.Net;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Hko;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class HkoApiClientTests
{
    [Fact]
    public async Task GetAlertBundleAsync_FetchesEveryOfficialHongKongAlertFeed()
    {
        var now = DateTimeOffset.Parse("2026-10-23T12:00:00Z");
        var handler = new HkoHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.weather.gov.hk/weatherAPI/opendata/")
        };
        var client = new HkoApiClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(new HkoOptions()),
            new FixedTimeProvider(now));

        var result = await client.GetAlertBundleAsync(CancellationToken.None);

        Assert.Equal(now, result.FetchedAt);
        Assert.Equal(4, handler.Requests.Count);
        Assert.Contains(handler.Requests, uri => uri.Contains("dataType=warnsum"));
        Assert.Contains(handler.Requests, uri => uri.Contains("dataType=warningInfo"));
        Assert.Contains(handler.Requests, uri => uri.Contains("dataType=swt"));
        Assert.Contains(handler.Requests, uri => uri.Contains("dataType=feltearthquake"));
        Assert.All(handler.Requests, uri => Assert.Contains("lang=tc", uri));
        Assert.All(handler.UserAgents, value => Assert.Contains("TravelInfoAssistant", value));
        Assert.All(handler.Accepts, value => Assert.Contains("application/json", value));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class HkoHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> UserAgents { get; } = [];
        public List<string> Accepts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            Accepts.Add(request.Headers.Accept.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
        }
    }
}
