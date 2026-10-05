using System.IO.Compression;
using System.Text;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Providers.Kmb;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class KmbTransitMapperTests
{
    [Fact]
    public void MapsOnlyRegularRoutesWithPublicDirections()
    {
        IReadOnlyList<KmbRouteRow> rows =
        [
            Route("1A", "O", "1", "中秀茂坪", "尖沙咀碼頭"),
            Route("1A", "I", "1", "尖沙咀碼頭", "中秀茂坪"),
            Route("1A", "O", "2", "特別班", "尖沙咀碼頭")
        ];

        var route = Assert.Single(KmbTransitMapper.MapRoutes(rows));

        Assert.Equal("1A", route.NameZh);
        Assert.Collection(
            route.Directions,
            outbound =>
            {
                Assert.Equal(0, outbound.Direction);
                Assert.Equal("尖沙咀碼頭", outbound.DestinationName);
            },
            inbound =>
            {
                Assert.Equal(1, inbound.Direction);
                Assert.Equal("中秀茂坪", inbound.DestinationName);
            });
    }

    [Fact]
    public void PreservesOfficialStopSequenceForEachDirection()
    {
        IReadOnlyList<KmbRouteStopRow> routeStops =
        [
            RouteStop("1A", "O", "1", "2", "B"),
            RouteStop("1A", "I", "1", "1", "B"),
            RouteStop("1A", "O", "1", "1", "A")
        ];
        IReadOnlyList<KmbStopRow> stops =
        [
            Stop("A", "中秀茂坪", "22.31", "114.23"),
            Stop("B", "秀茂坪邨", "22.32", "114.24")
        ];

        var result = KmbTransitMapper.MapStops("1A", 0, routeStops, stops);

        Assert.Equal(["A", "B"], result.Select(item => item.Id));
        Assert.Equal([1, 2], result.Select(item => item.Sequence));
        Assert.All(result, item => Assert.Equal(0, item.Direction));
    }

    [Fact]
    public void ReturnsAtMostThreeEtaRowsForTheSelectedDirection()
    {
        var timestamp = DateTimeOffset.Parse("2026-10-04T17:37:00+08:00");
        IReadOnlyList<KmbEtaRow> rows =
        [
            Eta("O", 1, timestamp.AddMinutes(1)),
            Eta("I", 1, timestamp.AddMinutes(2)),
            Eta("O", 2, timestamp.AddMinutes(3)),
            Eta("O", 3, timestamp.AddMinutes(5)),
            Eta("O", 4, timestamp.AddMinutes(7))
        ];

        var result = KmbTransitMapper.MapArrivals("1A", 0, "A", "中秀茂坪", rows);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.Equal(0, item.Direction));
        Assert.Equal(timestamp.AddMinutes(1), result[0].EstimatedAt);
        Assert.Equal(timestamp.AddMinutes(5), result[2].EstimatedAt);
    }

    [Fact]
    public void ParsesGtfsAndUsesTheLastPublishedOriginDeparture()
    {
        var schedule = HongKongGtfsParser.Parse(BuildGtfs());
        var calculator = new HongKongGtfsScheduleProvider(null!, null!, null!, TimeProvider.System);

        var lastDeparture = calculator.FindLastOriginDeparture(
            "KMB",
            "1A",
            0,
            DateTimeOffset.Parse("2026-10-05T01:00:00+08:00"),
            schedule);

        Assert.Equal(DateTimeOffset.Parse("2026-10-05T00:55:00+08:00"), lastDeparture);
        Assert.Equal(4, schedule.Routes.Count);
        Assert.Contains(schedule.Routes, item => item.AgencyId == "CTB");
        Assert.Contains(schedule.Routes, item => item.AgencyId == "NLB");
        Assert.Contains(schedule.Routes, item =>
            item.AgencyId == "TRAM" && item.LongName == "筲箕灣 - 上環(西港城)");
        Assert.Contains(schedule.Stops!, item =>
            item.Id == "99310" && item.Latitude == 22.281 && item.Longitude == 114.229);
    }

    private static KmbRouteRow Route(
        string route,
        string bound,
        string serviceType,
        string origin,
        string destination) => new()
    {
        Route = route,
        Bound = bound,
        ServiceType = serviceType,
        OriginZh = origin,
        DestinationZh = destination
    };

    private static KmbRouteStopRow RouteStop(
        string route,
        string bound,
        string serviceType,
        string sequence,
        string stopId) => new()
    {
        Route = route,
        Bound = bound,
        ServiceType = serviceType,
        Sequence = sequence,
        StopId = stopId
    };

    private static KmbStopRow Stop(string id, string name, string latitude, string longitude) => new()
    {
        StopId = id,
        NameZh = name,
        Latitude = latitude,
        Longitude = longitude
    };

    private static KmbEtaRow Eta(string direction, int sequence, DateTimeOffset estimatedAt) => new()
    {
        Route = "1A",
        Direction = direction,
        ServiceType = 1,
        EtaSequence = sequence,
        DestinationZh = "尖沙咀碼頭",
        EstimatedAt = estimatedAt,
        DataTimestamp = estimatedAt.AddMinutes(-1)
    };

    private static byte[] BuildGtfs()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "routes.txt", "route_id,agency_id,route_short_name,route_long_name\n1053,KMB,1A,\n9999,CTB,1A,\n1723,NLB,1,\n4001,TRAM,,筲箕灣 - 上環(西港城)\n");
            Add(archive, "trips.txt", "route_id,service_id,trip_id,direction_id\n1053,WEEKDAY,trip-1,0\n4001,WEEKDAY,4001-1-WEEKDAY-0542,\n");
            Add(archive, "frequencies.txt", "trip_id,start_time,end_time,headway_secs\ntrip-1,23:55:00,25:00:00,600\n4001-1-WEEKDAY-0542,05:42:00,06:00:00,600\n");
            Add(archive, "stop_times.txt", "trip_id,departure_time,stop_id,stop_sequence\ntrip-1,23:55:00,A,1\n4001-1-WEEKDAY-0542,05:42:00,99310,1\n");
            Add(archive, "stops.txt", "stop_id,stop_name,stop_lat,stop_lon\nA,巴士站,22.300,114.200\n99310,筲箕灣總站,22.281,114.229\n");
            Add(archive, "calendar.txt", "service_id,monday,tuesday,wednesday,thursday,friday,saturday,sunday,start_date,end_date\nWEEKDAY,0,0,0,0,0,0,1,20261001,20261031\n");
            Add(archive, "calendar_dates.txt", "service_id,date,exception_type\n");
        }
        return output.ToArray();
    }

    private static void Add(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
