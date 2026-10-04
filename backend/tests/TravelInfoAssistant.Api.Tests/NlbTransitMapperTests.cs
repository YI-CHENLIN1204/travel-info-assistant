using TravelInfoAssistant.Api.Providers.Nlb;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class NlbTransitMapperTests
{
    [Fact]
    public void KeepsEachOfficialRouteVariantDistinct()
    {
        var routes = NlbTransitMapper.MapRoutes(
        [
            new NlbRouteRow
            {
                RouteId = "1",
                RouteNumber = "1",
                NameZh = "梅窩碼頭 > 大澳"
            },
            new NlbRouteRow
            {
                RouteId = "2",
                RouteNumber = "1",
                NameZh = "大澳 > 梅窩碼頭"
            }
        ]);

        Assert.Equal(2, routes.Count);
        Assert.Equal(["NLB:1", "NLB:2"], routes.Select(item => item.QueryId).Order());
        Assert.All(routes, item => Assert.Single(item.Directions));
        Assert.Contains(routes, item => item.OriginName == "梅窩碼頭" && item.DestinationName == "大澳");
    }

    [Fact]
    public void PreservesOfficialStopOrder()
    {
        var stops = NlbTransitMapper.MapStops(
        [
            Stop("B", "第二站"),
            Stop("A", "第一站")
        ]);

        Assert.Equal(["B", "A"], stops.Select(item => item.Id));
        Assert.Equal([1, 2], stops.Select(item => item.Sequence));
    }

    [Fact]
    public void ReturnsAtMostThreeUpcomingEtaRows()
    {
        var now = DateTimeOffset.Parse("2026-10-05T12:00:00+08:00");
        var rows = new[]
        {
            Eta(now.AddMinutes(1), departed: 1),
            Eta(now.AddMinutes(2)),
            Eta(now.AddMinutes(4)),
            Eta(now.AddMinutes(6))
        };

        var arrivals = NlbTransitMapper.MapArrivals(
            "1",
            "1",
            "大澳",
            "A",
            "梅窩碼頭",
            now,
            rows);

        Assert.Equal(3, arrivals.Count);
        Assert.Equal(now.AddMinutes(1), arrivals[0].EstimatedAt);
        Assert.Equal(now.AddMinutes(4), arrivals[2].EstimatedAt);
        Assert.All(arrivals, item => Assert.Equal("大澳", item.DestinationName));
    }

    private static NlbStopRow Stop(string id, string name) => new()
    {
        StopId = id,
        NameZh = name,
        Latitude = "22.25",
        Longitude = "113.95"
    };

    private static NlbEtaRow Eta(DateTimeOffset estimatedAt, int departed = 0) => new()
    {
        EstimatedArrivalTime = estimatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        RouteVariantName = "梅窩碼頭 > 大澳",
        Departed = departed,
        GenerateTime = estimatedAt.AddMinutes(-1).ToString("yyyy-MM-dd HH:mm:ss")
    };
}
