using System.Net;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Jma;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class JmaApiClientTests
{
    [Fact]
    public async Task GetTokyoAlertDocumentsAsync_FetchesTokyoWeatherAndRecentHazardsOnly()
    {
        var now = DateTimeOffset.Parse("2026-10-23T12:00:00Z");
        var handler = new JmaHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.data.jma.go.jp/developer/xml/")
        };
        var client = new JmaApiClient(
            new StubHttpClientFactory(httpClient),
            Options.Create(new JmaOptions()),
            new FixedTimeProvider(now));

        var result = await client.GetTokyoAlertDocumentsAsync(CancellationToken.None);

        Assert.Equal(now, result.FetchedAt);
        Assert.Equal(2, result.Documents.Count);
        Assert.Contains(result.Documents, document => document.SourceUrl.EndsWith("tokyo.xml"));
        Assert.Contains(result.Documents, document => document.SourceUrl.EndsWith("quake.xml"));
        Assert.DoesNotContain(handler.Requests, uri => uri.EndsWith("osaka.xml"));
        Assert.All(handler.UserAgents, value => Assert.Contains("TravelInfoAssistant", value));
        Assert.All(handler.Accepts, value => Assert.Contains("application/xml", value));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class JmaHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> UserAgents { get; } = [];
        public List<string> Accepts { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.AbsoluteUri;
            Requests.Add(uri);
            UserAgents.Add(request.Headers.UserAgent.ToString());
            Accepts.Add(request.Headers.Accept.ToString());

            var body = uri switch
            {
                var value when value.EndsWith("feed/extra_l.xml") => WeatherFeed,
                var value when value.EndsWith("feed/eqvol_l.xml") => HazardFeed,
                _ => "<Report />"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private const string WeatherFeed = """
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry><title>気象特別警報・警報・注意報</title><updated>2026-10-23T11:55:00Z</updated>
            <link type="application/xml" href="https://www.data.jma.go.jp/developer/xml/data/tokyo.xml" />
            <content>【東京都気象警報・注意報】雷に注意してください。</content></entry>
          <entry><title>気象特別警報・警報・注意報</title><updated>2026-10-23T11:56:00Z</updated>
            <link type="application/xml" href="https://www.data.jma.go.jp/developer/xml/data/osaka.xml" />
            <content>【大阪府気象警報・注意報】雷に注意してください。</content></entry>
        </feed>
        """;

    private const string HazardFeed = """
        <feed xmlns="http://www.w3.org/2005/Atom">
          <entry><title>震源・震度に関する情報</title><updated>2026-10-23T11:50:00Z</updated>
            <link type="application/xml" href="https://www.data.jma.go.jp/developer/xml/data/quake.xml" />
            <content>【震源・震度情報】地震がありました。</content></entry>
          <entry><title>震源・震度に関する情報</title><updated>2026-10-21T11:50:00Z</updated>
            <link type="application/xml" href="https://www.data.jma.go.jp/developer/xml/data/old-quake.xml" />
            <content>【震源・震度情報】地震がありました。</content></entry>
        </feed>
        """;
}
