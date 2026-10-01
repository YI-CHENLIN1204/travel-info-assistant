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
