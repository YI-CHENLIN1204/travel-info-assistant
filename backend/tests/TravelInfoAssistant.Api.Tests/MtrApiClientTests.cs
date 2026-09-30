using System.Net;
using System.Text;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Mtr;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MtrApiClientTests
{
    [Fact]
    public async Task RequestsPublicEndpointsAndParsesOfficialCsv()
    {
        const string csv = """
            "Line Code","Direction","Station Code","Station ID","Chinese Name","English Name","Sequence"
            "TWL","UT","TST","3","尖沙咀","Tsim Sha Tsui",1.00
            "TWL","UT","ADM","2","金鐘","Admiralty",2.00
            """;
        const string json = """
            {
              "sys_time": "2026-10-01 12:00:00",
              "curr_time": "2026-10-01 11:59:58",
              "data": {},
              "isdelay": "N",
              "status": 1,
              "message": "successful"
            }
            """;
        var staticHandler = new RecordingHandler(csv, "text/csv");
        var realtimeHandler = new RecordingHandler(json, "application/json");
        using var staticClient = new HttpClient(staticHandler)
        {
            BaseAddress = new Uri("https://opendata.mtr.test/data/")
        };
        using var realtimeClient = new HttpClient(realtimeHandler)
        {
            BaseAddress = new Uri("https://rt.data.test/v1/transport/mtr/")
        };
        var client = new MtrApiClient(
            new StubHttpClientFactory(staticClient, realtimeClient),
            Microsoft.Extensions.Options.Options.Create(new MtrOptions()),
            TimeProvider.System);

        var network = await client.GetLinesAndStationsAsync(CancellationToken.None);
        var schedule = await client.GetScheduleAsync("TWL", "TST", CancellationToken.None);

        Assert.Equal(2, network.Data.Count);
        Assert.Equal("尖沙咀", network.Data[0].NameZh);
        Assert.Equal(1, network.Data[0].Sequence);
        Assert.Equal(1, schedule.Data.Status);
        Assert.Equal(
            "https://opendata.mtr.test/data/mtr_lines_and_stations.csv",
            Assert.Single(staticHandler.RequestUris).AbsoluteUri);
        var realtimeUri = Assert.Single(realtimeHandler.RequestUris);
        Assert.Equal("/v1/transport/mtr/getSchedule.php", realtimeUri.AbsolutePath);
        Assert.Contains("line=TWL", realtimeUri.Query, StringComparison.Ordinal);
        Assert.Contains("sta=TST", realtimeUri.Query, StringComparison.Ordinal);
        Assert.Contains("lang=TC", realtimeUri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("key", realtimeUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpClientFactory(
        HttpClient staticClient,
        HttpClient realtimeClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name switch
        {
            "mtr-static" => staticClient,
            "mtr-realtime" => realtimeClient,
            _ => throw new InvalidOperationException($"Unexpected client name: {name}")
        };
    }

    private sealed class RecordingHandler(string content, string mediaType) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(Assert.IsType<Uri>(request.RequestUri));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, mediaType)
            });
        }
    }
}
