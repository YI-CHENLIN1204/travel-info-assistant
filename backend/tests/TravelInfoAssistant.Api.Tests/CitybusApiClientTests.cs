using System.Net;
using System.Text;
using TravelInfoAssistant.Api.Providers.Citybus;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class CitybusApiClientTests
{
    [Fact]
    public async Task RequestsOfficialRouteStopAndEtaEndpoints()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://rt.data.test/v1/transport/citybus-nwfb/")
        };
        var client = new CitybusApiClient(new StubHttpClientFactory(httpClient), TimeProvider.System);

        var routes = await client.GetRoutesAsync(CancellationToken.None);
        var routeStops = await client.GetRouteStopsAsync("1", 0, CancellationToken.None);
        var stop = await client.GetStopAsync("001027", CancellationToken.None);
        var eta = await client.GetEtaAsync("001027", "1", CancellationToken.None);

        Assert.Equal("1", Assert.Single(routes.Data).Route);
        Assert.Equal("001027", Assert.Single(routeStops.Data).StopId);
        Assert.Equal("跑馬地", stop.Data.NameZh);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T12:03:00+08:00"), Assert.Single(eta.Data).EstimatedAt);
        Assert.Equal(
            [
                "/v1/transport/citybus-nwfb/route/ctb",
                "/v1/transport/citybus-nwfb/route-stop/ctb/1/outbound",
                "/v1/transport/citybus-nwfb/stop/001027",
                "/v1/transport/citybus-nwfb/eta/ctb/001027/1"
            ],
            handler.RequestUris.Select(item => item.AbsolutePath));
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name == "citybus-api"
            ? client
            : throw new InvalidOperationException($"Unexpected client name: {name}");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = Assert.IsType<Uri>(request.RequestUri);
            RequestUris.Add(uri);
            var json = uri.AbsolutePath switch
            {
                var path when path.EndsWith("/route/ctb", StringComparison.Ordinal) =>
                    """{"data":[{"route":"1","orig_tc":"中環","dest_tc":"跑馬地","data_timestamp":"2026-10-05T05:00:00+08:00"}]}""",
                var path when path.Contains("/route-stop/", StringComparison.Ordinal) =>
                    """{"data":[{"route":"1","dir":"O","seq":1,"stop":"001027","data_timestamp":"2026-10-05T05:00:00+08:00"}]}""",
                var path when path.Contains("/stop/", StringComparison.Ordinal) =>
                    """{"data":{"stop":"001027","name_tc":"跑馬地","name_en":"Happy Valley","lat":"22.27","long":"114.18","data_timestamp":"2026-10-05T05:00:00+08:00"}}""",
                _ =>
                    """{"data":[{"route":"1","dir":"O","eta_seq":1,"dest_tc":"跑馬地","eta":"2026-10-05T12:03:00+08:00","rmk_tc":"","data_timestamp":"2026-10-05T12:00:00+08:00"}]}"""
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
