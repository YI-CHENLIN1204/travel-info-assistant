using TravelInfoAssistant.Api.Providers.Citybus;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class CitybusTransitMapperTests
{
    [Fact]
    public void MapsRouteWithOperatorQualifiedQueryIdAndTwoDirections()
    {
        var route = Assert.Single(CitybusTransitMapper.MapRoutes(
        [
            new CitybusRouteRow
            {
                Route = "1",
                OriginZh = "中環 (港澳碼頭)",
                DestinationZh = "跑馬地 (上)"
            }
        ]));

        Assert.Equal("CTB:1", route.Id);
        Assert.Equal("CTB:1", route.QueryId);
        Assert.Equal(["城巴"], route.Operators);
        Assert.Collection(
            route.Directions,
            outbound => Assert.Equal("跑馬地 (上)", outbound.DestinationName),
            inbound => Assert.Equal("中環 (港澳碼頭)", inbound.DestinationName));
    }

    [Fact]
    public void PreservesOfficialStopSequenceForSelectedDirection()
    {
        IReadOnlyList<CitybusRouteStopRow> routeStops =
        [
            new() { Route = "1", Direction = "O", Sequence = 2, StopId = "B" },
            new() { Route = "1", Direction = "I", Sequence = 1, StopId = "B" },
            new() { Route = "1", Direction = "O", Sequence = 1, StopId = "A" }
        ];
        IReadOnlyList<CitybusStopRow> stops =
        [
            new() { StopId = "A", NameZh = "跑馬地", Latitude = "22.27", Longitude = "114.18" },
            new() { StopId = "B", NameZh = "禮頓道", Latitude = "22.28", Longitude = "114.19" }
        ];

        var result = CitybusTransitMapper.MapStops("1", 0, routeStops, stops);

        Assert.Equal(["A", "B"], result.Select(item => item.Id));
        Assert.Equal([1, 2], result.Select(item => item.Sequence));
        Assert.All(result, item => Assert.Equal(0, item.Direction));
    }

    [Fact]
    public void ReturnsAtMostThreeEtaRowsForSelectedDirection()
    {
        var now = DateTimeOffset.Parse("2026-10-05T12:00:00+08:00");
        IReadOnlyList<CitybusEtaRow> rows =
        [
            Eta("O", 1, now.AddMinutes(2)),
            Eta("I", 1, now.AddMinutes(3)),
            Eta("O", 2, now.AddMinutes(5)),
            Eta("O", 3, now.AddMinutes(8)),
            Eta("O", 4, now.AddMinutes(12))
        ];

        var result = CitybusTransitMapper.MapArrivals("1", 0, "A", "跑馬地", rows);

        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.Equal(0, item.Direction));
        Assert.Equal(now.AddMinutes(2), result[0].EstimatedAt);
        Assert.Equal(now.AddMinutes(8), result[2].EstimatedAt);
    }

    private static CitybusEtaRow Eta(string direction, int sequence, DateTimeOffset estimatedAt) => new()
    {
        Route = "1",
        Direction = direction,
        EtaSequence = sequence,
        DestinationZh = "跑馬地 (上)",
        EstimatedAt = estimatedAt,
        DataTimestamp = estimatedAt.AddMinutes(-1)
    };
}
