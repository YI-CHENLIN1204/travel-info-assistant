using TravelInfoAssistant.Api.Providers.LtaDataMall;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaDataMallBusTransitMapperTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T12:00:00+08:00");

    [Fact]
    public void MapsRoutesDirectionsAndStopsInOfficialSequence()
    {
        var network = CreateNetwork();

        var route = Assert.Single(LtaDataMallBusTransitMapper.MapRoutes(network));
        var outbound = LtaDataMallBusTransitMapper.MapStops(route.QueryId!, 0, network);
        var inbound = LtaDataMallBusTransitMapper.MapStops(route.QueryId!, 1, network);

        Assert.Equal("LTA-BUS:SBST:36", route.QueryId);
        Assert.Equal([0, 1], route.Directions.Select(item => item.Direction).ToArray());
        Assert.Equal("Airport", route.Directions[0].DestinationName);
        Assert.Equal(["City Hall", "Orchard", "Airport"], outbound.Select(item => item.NameZh));
        Assert.Equal(["Airport", "Orchard", "City Hall"], inbound.Select(item => item.NameZh));
        Assert.Equal([1, 2, 3], outbound.Select(item => item.Sequence));
    }

    [Fact]
    public void MapsRealtimeAndScheduledArrivalWithoutMislabelingScheduleAsRealtime()
    {
        var response = new LtaBusArrivalResponse
        {
            BusStopCode = "01012",
            Services =
            [
                new LtaBusArrivalService
                {
                    ServiceNo = "36",
                    Operator = "SBST",
                    NextBus = new LtaBusArrivalEstimate
                    {
                        DestinationCode = "03000",
                        EstimatedArrival = "2026-10-01T12:03:00+08:00",
                        Monitored = 1
                    },
                    NextBus2 = new LtaBusArrivalEstimate
                    {
                        DestinationCode = "03000",
                        EstimatedArrival = "2026-10-01T12:12:00+08:00",
                        Monitored = 0
                    }
                }
            ]
        };

        var arrivals = LtaDataMallBusTransitMapper.MapArrivals(
            "LTA-BUS:SBST:36",
            0,
            "01012",
            response,
            CreateNetwork(),
            Now);

        Assert.Equal(2, arrivals.Count);
        Assert.Equal("即時預估", arrivals[0].ServiceStatus);
        Assert.Equal(arrivals[0].ScheduledAt, arrivals[0].EstimatedAt);
        Assert.Equal("表定時間", arrivals[1].ServiceStatus);
        Assert.Null(arrivals[1].EstimatedAt);
        Assert.Equal("Airport", arrivals[0].DestinationName);
    }

    [Fact]
    public void ReportsOfficialLastBusOnlyAfterTheServiceDayEnds()
    {
        var network = CreateNetwork();

        var before = LtaDataMallBusTransitMapper.GetEndedServiceDayLastDeparture(
            "LTA-BUS:SBST:36",
            0,
            "01012",
            network,
            DateTimeOffset.Parse("2026-10-01T23:20:00+08:00"));
        var after = LtaDataMallBusTransitMapper.GetEndedServiceDayLastDeparture(
            "LTA-BUS:SBST:36",
            0,
            "01012",
            network,
            DateTimeOffset.Parse("2026-10-01T23:40:00+08:00"));

        Assert.Null(before);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T23:30:00+08:00"), after);
    }

    internal static LtaBusNetwork CreateNetwork() =>
        new(
            [
                Service("36", "SBST", 1, "01012", "03000"),
                Service("36", "SBST", 2, "03000", "01012")
            ],
            [
                Route(1, 1, "01012"),
                Route(1, 2, "02000"),
                Route(1, 3, "03000"),
                Route(2, 1, "03000"),
                Route(2, 2, "02000"),
                Route(2, 3, "01012")
            ],
            [
                Stop("01012", "City Hall"),
                Stop("02000", "Orchard"),
                Stop("03000", "Airport")
            ]);

    private static LtaBusServiceRow Service(
        string serviceNo,
        string operatorCode,
        int direction,
        string origin,
        string destination) =>
        new()
        {
            ServiceNo = serviceNo,
            Operator = operatorCode,
            Direction = direction,
            OriginCode = origin,
            DestinationCode = destination
        };

    private static LtaBusRouteRow Route(int direction, int sequence, string stopId) =>
        new()
        {
            ServiceNo = "36",
            Operator = "SBST",
            Direction = direction,
            StopSequence = sequence,
            BusStopCode = stopId,
            WD_FirstBus = "0600",
            WD_LastBus = "2330",
            SAT_FirstBus = "0600",
            SAT_LastBus = "2330",
            SUN_FirstBus = "0600",
            SUN_LastBus = "2330"
        };

    private static LtaBusStopRow Stop(string id, string name) =>
        new()
        {
            BusStopCode = id,
            RoadName = name,
            Description = name,
            Latitude = 1.3,
            Longitude = 103.8
        };
}
