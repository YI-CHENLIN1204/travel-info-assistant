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
        await client.GetRailwaysAsync(
            "odpt.Operator:TokyoMetro",
            CancellationToken.None);
        await client.GetStationsAsync(
            "odpt.Operator:Toei",
            CancellationToken.None);
        await client.GetStationsByIdsAsync(
            [
                "odpt.Station:Keisei.Main.KeiseiTakasago",
                "odpt.Station:Hokuso.Hokuso.ImbaNihonIdai"
            ],
            CancellationToken.None);
        await client.GetStationTimetablesAsync(
            "odpt.Operator:Toei",
            "odpt.Station:Toei.Asakusa.Asakusa",
            CancellationToken.None);
        await client.GetTrainInformationAsync(
            "odpt.Operator:Toei",
            CancellationToken.None);

        Assert.Collection(
            handler.RequestUris,
            uri => AssertRequest(uri, "/api/v4/odpt:Calendar"),
            uri => AssertRequest(
                uri,
                "/api/v4/odpt:Railway",
                ("odpt:operator", "odpt.Operator:TokyoMetro")),
            uri => AssertRequest(
                uri,
                "/api/v4/odpt:Station",
                ("odpt:operator", "odpt.Operator:Toei")),
            uri => AssertRequest(
                uri,
                "/api/v4/odpt:Station",
                (
                    "owl:sameAs",
                    "odpt.Station:Keisei.Main.KeiseiTakasago," +
                    "odpt.Station:Hokuso.Hokuso.ImbaNihonIdai")),
            uri => AssertRequest(
                uri,
                "/api/v4/odpt:StationTimetable",
                ("odpt:operator", "odpt.Operator:Toei"),
                ("odpt:station", "odpt.Station:Toei.Asakusa.Asakusa")),
            uri => AssertRequest(
                uri,
                "/api/v4/odpt:TrainInformation",
                ("odpt:operator", "odpt.Operator:Toei")));
    }

    private static void AssertRequest(
        Uri uri,
        string expectedPath,
        params (string Key, string Value)[] expectedQuery)
    {
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("api.odpt.test", uri.Host);
        Assert.Equal(expectedPath, uri.AbsolutePath);
        Assert.Contains(
            "acl%3AconsumerKey=not-a-real-key",
            uri.Query,
            StringComparison.OrdinalIgnoreCase);
        foreach (var (key, value) in expectedQuery)
        {
            Assert.Contains(
                $"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}",
                uri.Query,
                StringComparison.OrdinalIgnoreCase);
        }
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
