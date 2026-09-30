using System.Net;
using System.Text;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Odpt;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class OdptApiClientTests
{
    [Fact]
    public async Task RequestsResolveAgainstConfiguredHttpsBaseAddress()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.odpt.test/api/v4/")
        };
        var client = new OdptApiClient(
            new StubHttpClientFactory(httpClient),
            Microsoft.Extensions.Options.Options.Create(
                new OdptOptions { ConsumerKey = "not-a-real-key" }),
            TimeProvider.System);

        await client.GetCalendarsAsync(CancellationToken.None);
        await client.GetRailwaysAsync(CancellationToken.None);
        await client.GetStationsAsync(CancellationToken.None);
        await client.GetStationTimetablesAsync(
            "odpt.Station:TokyoMetro.Ginza.Ueno",
            CancellationToken.None);
        await client.GetTrainInformationAsync(CancellationToken.None);

        Assert.Collection(
            handler.RequestUris,
            uri => AssertRequest(uri, "/api/v4/odpt:Calendar"),
            uri => AssertRequest(uri, "/api/v4/odpt:Railway"),
            uri => AssertRequest(uri, "/api/v4/odpt:Station"),
            uri => AssertRequest(uri, "/api/v4/odpt:StationTimetable"),
            uri => AssertRequest(uri, "/api/v4/odpt:TrainInformation"));
    }

    private static void AssertRequest(Uri uri, string expectedPath)
    {
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("api.odpt.test", uri.Host);
        Assert.Equal(expectedPath, uri.AbsolutePath);
        Assert.Contains(
            "acl%3AconsumerKey=not-a-real-key",
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("odpt-api", name);
            return client;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(Assert.IsType<Uri>(request.RequestUri));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            });
        }
    }
}
