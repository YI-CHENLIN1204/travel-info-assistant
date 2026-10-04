using System.Net;
using System.Text;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Mtr;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MtrApiClientTests
{
    [Fact]
    public async Task RequestsPublicEndpointsAndParsesOfficialCsvJsonAndServiceHoursHtml()
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
        const string html = """
            <h2 class="trainLine TWL"></h2>
            <table>
              <tr><th>To</th><th>First</th><th>Last</th></tr>
              <tr>
                <td><span class="js_station_25">-</span></td>
                <td class="firstTrain">0606</td>
                <td>0054</td>
              </tr>
              <!-- <tr><td><span class="js_station_99">-</span></td><td>0600</td><td>0015</td></tr> -->
            </table>
            <h2 class="trainLine ISL"></h2>
            <table>
              <tr><td><span class="js_station_83">-</span></td><td>0607</td><td>0104</td></tr>
            </table>
            """;
        var staticHandler = new RecordingHandler(csv, "text/csv");
        var realtimeHandler = new RecordingHandler(json, "application/json");
        var serviceHoursHandler = new RecordingHandler(html, "text/html");
        using var staticClient = new HttpClient(staticHandler)
        {
            BaseAddress = new Uri("https://opendata.mtr.test/data/")
        };
        using var realtimeClient = new HttpClient(realtimeHandler)
        {
            BaseAddress = new Uri("https://rt.data.test/v1/transport/mtr/")
        };
        using var serviceHoursClient = new HttpClient(serviceHoursHandler)
        {
            BaseAddress = new Uri("https://www.mtr.test/ch/customer/services/")
        };
        var client = new MtrApiClient(
            new StubHttpClientFactory(staticClient, realtimeClient, serviceHoursClient),
            Microsoft.Extensions.Options.Options.Create(new MtrOptions()),
            TimeProvider.System);

        var network = await client.GetLinesAndStationsAsync(CancellationToken.None);
        var schedule = await client.GetScheduleAsync("TWL", "TST", CancellationToken.None);
        var lastTrains = await client.GetLastTrainSchedulesAsync("1", CancellationToken.None);

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
        Assert.Collection(
            lastTrains.Data,
            first =>
            {
                Assert.Equal("TWL", first.LineCode);
                Assert.Equal("25", first.DestinationStationId);
                Assert.Equal("0054", first.DepartureTime);
            },
            second =>
            {
                Assert.Equal("ISL", second.LineCode);
                Assert.Equal("83", second.DestinationStationId);
                Assert.Equal("0104", second.DepartureTime);
            });
        var serviceHoursUri = Assert.Single(serviceHoursHandler.RequestUris);
        Assert.Equal(
            "/ch/customer/services/service_hours_search.php",
            serviceHoursUri.AbsolutePath);
        Assert.Contains("station=1", serviceHoursUri.Query, StringComparison.Ordinal);
        Assert.Contains("query_type=search", serviceHoursUri.Query, StringComparison.Ordinal);
    }

    private sealed class StubHttpClientFactory(
        HttpClient staticClient,
        HttpClient realtimeClient,
        HttpClient serviceHoursClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name switch
        {
            "mtr-static" => staticClient,
            "mtr-realtime" => realtimeClient,
            "mtr-service-hours" => serviceHoursClient,
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
