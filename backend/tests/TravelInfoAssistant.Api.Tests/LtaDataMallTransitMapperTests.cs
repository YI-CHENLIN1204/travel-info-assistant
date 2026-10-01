using TravelInfoAssistant.Api.Providers.LtaDataMall;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaDataMallTransitMapperTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T04:00:00Z");

    [Fact]
    public void MapsRoutesAndStationsWithoutRenumberingOfficialDirections()
    {
        var network = CreateNetwork();

        var route = Assert.Single(LtaDataMallTransitMapper.MapRoutes(network));
        var stations = LtaDataMallTransitMapper.MapStations(network);

        Assert.Equal("LTA:NS", route.Id);
        Assert.Equal("南北線", route.NameZh);
        Assert.Equal([5, 7], route.Directions.Select(item => item.Direction).Order().ToArray());
        Assert.Equal(["烏節", "政府大廈"], route.StationNames);
        Assert.Contains(stations, station =>
            station.Id == "LTA:NS:NS22" &&
            station.NameZh == "烏節" &&
            station.NameEn == "Orchard" &&
            station.Code == "NS22");
    }

    [Fact]
    public void ConsolidatesGtfsServicePatternsIntoOnePublicLine()
    {
        var network = new LtaGtfsNetwork(
            [
                new LtaGtfsRoute("CCL", "CC", "Circle Line", "SMRT"),
                new LtaGtfsRoute("CCL_FIRST", "CC", "Circle Line", "SMRT")
            ],
            [
                new LtaGtfsStop("CC1", "CC1", "Dhoby Ghaut", null, 1, null, 1.299, 103.845),
                new LtaGtfsStop("CC2", "CC2", "Bras Basah", null, 1, null, 1.297, 103.851)
            ],
            [],
            [
                new LtaGtfsRouteStop("CCL", "CC1", 0, 1),
                new LtaGtfsRouteStop("CCL", "CC2", 0, 2),
                new LtaGtfsRouteStop("CCL_FIRST", "CC1", 0, 1)
            ],
            [
                new LtaChineseStationName("CC1", "Dhoby Ghaut", "多美歌", "Circle Line", "环线"),
                new LtaChineseStationName("CC2", "Bras Basah", "百胜", "Circle Line", "环线")
            ]);

        var route = Assert.Single(LtaDataMallTransitMapper.MapRoutes(network));
        var stations = LtaDataMallTransitMapper.MapStations(network);

        Assert.Equal("LTA:CCL", route.Id);
        Assert.Equal(2, stations.Count);
        Assert.All(stations, station => Assert.Equal("LTA:CCL", station.RailwayId));
    }

    [Fact]
    public void FallsBackToActiveScheduledDeparturesWhenRealtimeFeedIsEmpty()
    {
        var network = CreateNetwork() with
        {
            Trips = [new LtaGtfsTrip("trip-5", "NS", "Marina South Pier", 5, "weekday")],
            StopTimes = [new LtaGtfsStopTime("trip-5", "NS22-P1", 1, "12:03:00", "12:03:00")],
            Calendars = [new LtaGtfsCalendar(
                "weekday",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                [DayOfWeek.Thursday])],
            CalendarDates = []
        };

        var arrivals = LtaDataMallTransitMapper.MapArrivals(
            "NS",
            "NS22",
            new LtaRealtimeFeed(Now, [], []),
            network,
            Now);

        var arrival = Assert.Single(arrivals);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 12, 3, 0, TimeSpan.FromHours(8)), arrival.ScheduledAt);
        Assert.Null(arrival.EstimatedAt);
        Assert.Equal("表定時間", arrival.ServiceStatus);
        Assert.Equal(5, arrival.Direction);
    }

    [Fact]
    public void IncludesNextServiceDateForDeparturesAfterMidnight()
    {
        var now = DateTimeOffset.Parse("2026-10-01T15:58:00Z");
        var network = CreateNetwork() with
        {
            Trips = [new LtaGtfsTrip("trip-5", "NS", "Marina South Pier", 5, "friday")],
            StopTimes = [new LtaGtfsStopTime("trip-5", "NS22-P1", 1, "00:03:00", "00:03:00")],
            Calendars = [new LtaGtfsCalendar(
                "friday",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                [DayOfWeek.Friday])],
            CalendarDates = []
        };

        var arrivals = LtaDataMallTransitMapper.MapArrivals(
            "NS",
            "NS22",
            new LtaRealtimeFeed(now, [], []),
            network,
            now);

        var arrival = Assert.Single(arrivals);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 2, 0, 3, 0, TimeSpan.FromHours(8)),
            arrival.ScheduledAt);
    }

    [Fact]
    public void KeepsScheduleLabelWhenTripUpdateDoesNotContainSelectedStation()
    {
        var network = CreateNetwork() with
        {
            Trips = [new LtaGtfsTrip("trip-5", "NS", "Marina South Pier", 5, "weekday")],
            StopTimes = [new LtaGtfsStopTime("trip-5", "NS22-P1", 1, "12:03:00", "12:03:00")],
            Calendars = [new LtaGtfsCalendar(
                "weekday",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                [DayOfWeek.Thursday])],
            CalendarDates = []
        };
        var feed = new LtaRealtimeFeed(
            Now,
            [TripUpdate("other-stop", "trip-5", "NS25", Now.AddMinutes(4), 60, 0)],
            []);

        var arrivals = LtaDataMallTransitMapper.MapArrivals(
            "NS",
            "NS22",
            feed,
            network,
            Now);

        var arrival = Assert.Single(arrivals);
        Assert.Equal("表定時間", arrival.ServiceStatus);
        Assert.Null(arrival.EstimatedAt);
    }

    [Fact]
    public void MapsFreshPredictionsDelayPlatformAndSkippedStop()
    {
        var network = CreateNetwork();
        var feed = new LtaRealtimeFeed(
            Now,
            [
                TripUpdate("prediction", "trip-5", "NS22-P1", Now.AddMinutes(3), 125, 0),
                TripUpdate("skipped", "trip-7", "NS22-P2", Now.AddMinutes(4), null, 1),
                TripUpdate("too-far", "trip-5", "NS22-P1", Now.AddHours(5), null, 0)
            ],
            []);

        var arrivals = LtaDataMallTransitMapper.MapArrivals(
            "NS",
            "NS22",
            feed,
            network,
            Now);

        Assert.Equal(2, arrivals.Count);
        Assert.Contains(arrivals, item =>
            item.ServiceStatus == "延誤 3 分鐘" &&
            item.Platform == "A" &&
            item.DestinationName == "Marina South Pier");
        Assert.Contains(arrivals, item =>
            item.ServiceStatus == "不停靠本站" && item.Platform == "B");
        Assert.All(arrivals, item => Assert.Equal(Now, item.SourceUpdatedAt));
    }

    [Fact]
    public void MapsOnlyActiveAlertsForSelectedRoute()
    {
        var network = CreateNetwork();
        var feed = new LtaRealtimeFeed(
            Now,
            [],
            [
                new LtaServiceAlert(
                    "active",
                    ["NS"],
                    [],
                    Now.AddMinutes(-10),
                    Now.AddMinutes(20),
                    "Train delay",
                    "Allow extra travel time.",
                    3),
                new LtaServiceAlert(
                    "expired",
                    ["NS"],
                    [],
                    Now.AddHours(-2),
                    Now.AddHours(-1),
                    "Old notice",
                    null,
                    1)
            ]);

        var status = LtaDataMallTransitMapper.MapStatus("NS", feed, network, Now);

        Assert.NotNull(status);
        Assert.Equal("LTA:NS", status.LineId);
        Assert.Contains("嚴重延誤", status.MessageEn);
        Assert.Contains("Train delay", status.MessageEn);
        Assert.DoesNotContain("Old notice", status.MessageEn);
        Assert.Equal(Now.AddSeconds(90), status.ValidUntil);
    }

    private static LtaTripUpdate TripUpdate(
        string id,
        string tripId,
        string stopId,
        DateTimeOffset time,
        int? delaySeconds,
        int stopRelationship) =>
        new(
            id,
            tripId,
            null,
            null,
            0,
            Now,
            [new LtaStopTimeUpdate(
                stopId,
                1,
                time,
                time,
                delaySeconds,
                delaySeconds,
                stopRelationship)]);

    private static LtaGtfsNetwork CreateNetwork() =>
        new(
            [new LtaGtfsRoute("NS", "NS", "North South Line", "SMRT")],
            [
                new LtaGtfsStop("NS22", "NS22", "Orchard", null, 1, null, 1.304, 103.832),
                new LtaGtfsStop("NS22-P1", "NS22", "Orchard Platform A", "NS22", 0, "A", 1.304, 103.832),
                new LtaGtfsStop("NS22-P2", "NS22", "Orchard Platform B", "NS22", 0, "B", 1.304, 103.832),
                new LtaGtfsStop("NS25", "NS25", "City Hall", null, 1, null, 1.293, 103.852)
            ],
            [
                new LtaGtfsTrip("trip-5", "NS", "Marina South Pier", 5),
                new LtaGtfsTrip("trip-7", "NS", "Jurong East", 7)
            ],
            [
                new LtaGtfsRouteStop("NS", "NS22", 5, 1),
                new LtaGtfsRouteStop("NS", "NS25", 5, 2),
                new LtaGtfsRouteStop("NS", "NS25", 7, 1),
                new LtaGtfsRouteStop("NS", "NS22", 7, 2)
            ],
            [
                new LtaChineseStationName("NS22", "Orchard", "烏節", "North South Line", "南北線"),
                new LtaChineseStationName("NS25", "City Hall", "政府大廈", "North South Line", "南北線")
            ]);
}
