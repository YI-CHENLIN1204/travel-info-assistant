using System.IO.Compression;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.LtaDataMall;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaDataMallApiClientTests
{
    [Fact]
    public async Task DownloadsOfficialScheduleWithoutForwardingAccountKey()
    {
        var schedule = CreateScheduleZip();
        var emptyZip = CreateZip([]);
        var apiHandler = new RecordingHandler(request =>
        {
            var endpoint = Assert.IsType<Uri>(request.RequestUri).Segments.Last();
            return JsonResponse($$"""
                {
                  "value": [{
                    "timestamp": "2026-10-01T04:00:00Z",
                    "link": "https://dmprod-datasets.s3.ap-southeast-1.amazonaws.com/{{endpoint}}?signature=test"
                  }]
                }
                """);
        });
        var downloadHandler = new RecordingHandler(request =>
        {
            var uri = Assert.IsType<Uri>(request.RequestUri);
            return BytesResponse(
                uri.Host == "datamall.lta.gov.sg" ? emptyZip : schedule,
                "application/zip");
        });
        using var apiClient = new HttpClient(apiHandler)
        {
            BaseAddress = new Uri("https://datamall2.mytransport.sg/ltaodataservice/")
        };
        using var downloadClient = new HttpClient(downloadHandler);
        var client = new LtaDataMallApiClient(
            new StubHttpClientFactory(apiClient, downloadClient),
            Microsoft.Extensions.Options.Options.Create(new LtaDataMallOptions
            {
                AccountKey = "test-account-key"
            }),
            TimeProvider.System,
            NullLogger<LtaDataMallApiClient>.Instance);

        var result = await client.GetNetworkAsync(CancellationToken.None);

        Assert.Single(result.Data.Routes);
        Assert.Equal(2, result.Data.Stops.Count);
        Assert.Equal("NS", result.Data.Trips.Single().RouteId);
        Assert.Equal("weekday", result.Data.Trips.Single().ServiceId);
        Assert.Equal("12:00:00", result.Data.StopTimes!.Single(item => item.StopId == "NS22").DepartureTime);
        Assert.Contains(DayOfWeek.Thursday, Assert.Single(result.Data.Calendars!).Days);
        Assert.All(apiHandler.Requests, request => Assert.True(request.HasAccountKey));
        Assert.All(downloadHandler.Requests, request => Assert.False(request.HasAccountKey));
        Assert.Contains(downloadHandler.Requests, request =>
            request.Host == "dmprod-datasets.s3.ap-southeast-1.amazonaws.com");
        Assert.Contains(downloadHandler.Requests, request =>
            request.Host == "datamall.lta.gov.sg");
    }

    [Fact]
    public async Task RejectsMissingAccountKeyBeforeCallingDataMall()
    {
        var apiHandler = new RecordingHandler(_ => throw new InvalidOperationException());
        var downloadHandler = new RecordingHandler(_ => BytesResponse(CreateZip([]), "application/zip"));
        using var apiClient = new HttpClient(apiHandler)
        {
            BaseAddress = new Uri("https://datamall2.mytransport.sg/ltaodataservice/")
        };
        using var downloadClient = new HttpClient(downloadHandler);
        var client = new LtaDataMallApiClient(
            new StubHttpClientFactory(apiClient, downloadClient),
            Microsoft.Extensions.Options.Options.Create(new LtaDataMallOptions()),
            TimeProvider.System,
            NullLogger<LtaDataMallApiClient>.Instance);

        await Assert.ThrowsAsync<LtaDataMallNotConfiguredException>(
            () => client.GetTripUpdatesAsync(CancellationToken.None));

        Assert.Empty(apiHandler.Requests);
    }

    private static byte[] CreateScheduleZip() => CreateZip(
    [
        ("routes.txt", "route_id,agency_id,route_short_name,route_long_name\nNS,SMRT,NS,North South Line\n"),
        ("stops.txt", "stop_id,stop_code,stop_name,location_type,stop_lat,stop_lon\nNS22,NS22,Orchard,1,1.304,103.832\nNS25,NS25,City Hall,1,1.293,103.852\n"),
        ("trips.txt", "route_id,service_id,trip_id,trip_headsign,direction_id\nNS,weekday,trip-1,Marina South Pier,0\n"),
        ("stop_times.txt", "trip_id,arrival_time,departure_time,stop_id,stop_sequence\ntrip-1,12:00:00,12:00:00,NS22,1\ntrip-1,12:05:00,12:05:00,NS25,2\n"),
        ("calendar.txt", "service_id,monday,tuesday,wednesday,thursday,friday,saturday,sunday,start_date,end_date\nweekday,1,1,1,1,1,0,0,20260101,20261231\n")
    ]);

    private static byte[] CreateZip(IReadOnlyList<(string Name, string Content)> files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(file.Content);
            }
        }
        return stream.ToArray();
    }

    private static HttpResponseMessage JsonResponse(string value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage BytesResponse(byte[] value, string mediaType)
    {
        var content = new ByteArrayContent(value);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class StubHttpClientFactory(
        HttpClient apiClient,
        HttpClient downloadClient) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => name switch
        {
            "lta-datamall" => apiClient,
            "lta-download" => downloadClient,
            _ => throw new InvalidOperationException($"Unexpected client name: {name}")
        };
    }

    private sealed record RecordedRequest(string Host, bool HasAccountKey);

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var uri = Assert.IsType<Uri>(request.RequestUri);
            Requests.Add(new RecordedRequest(
                uri.Host,
                request.Headers.Contains("AccountKey")));
            return Task.FromResult(responseFactory(request));
        }
    }
}
