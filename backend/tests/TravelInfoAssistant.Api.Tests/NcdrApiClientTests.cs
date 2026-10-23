using System.Net;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Ncdr;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class NcdrApiClientTests
{
    [Fact]
    public async Task GetActiveAlertsAsync_UsesEncodedCountyAndAtomHeaders()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://alerts.ncdr.nat.gov.tw/")
        };
        var now = DateTimeOffset.Parse("2026-10-23T13:00:00Z");
        var client = new NcdrApiClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(new NcdrOptions()),
            new FixedTimeProvider(now));

        var result = await client.GetActiveAlertsAsync("臺北市", CancellationToken.None);

        Assert.Equal("<feed />", result.Xml);
        Assert.Equal(now, result.FetchedAt);
        Assert.Equal(
            "https://alerts.ncdr.nat.gov.tw/webapi/RssAtomFeed.ashx?County=臺北市",
            Uri.UnescapeDataString(handler.RequestUri!.AbsoluteUri));
        Assert.Contains("application/atom+xml", handler.Accept);
        Assert.Contains("TravelInfoAssistant", handler.UserAgent);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string Accept { get; private set; } = string.Empty;
        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Accept = request.Headers.Accept.ToString();
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<feed />")
            });
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
