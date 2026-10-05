using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class HongKongTramTransitProviderTests
{
    [Fact]
    public async Task MapsDirectionsAndPreservesRealStopOrder()
    {
        var provider = CreateProvider(Schedule(), "2026-10-05T06:00:00+08:00");

        var routes = await provider.GetRoutesAsync(CancellationToken.None);
        var stops = await provider.GetStopsAsync("4001", 1, CancellationToken.None);

        var route = Assert.Single(routes.Data);
        Assert.Equal("筲箕灣 - 上環(西港城)", route.NameZh);
        Assert.Collection(
            route.Directions,
            direction =>
            {
                Assert.Equal(0, direction.Direction);
                Assert.Equal("筲箕灣總站", direction.OriginName);
                Assert.Equal("上環街市(西港城)總站", direction.DestinationName);
            },
            direction =>
            {
                Assert.Equal(1, direction.Direction);
                Assert.Equal("上環街市(西港城)總站", direction.OriginName);
                Assert.Equal("筲箕灣總站", direction.DestinationName);
            });
        Assert.Equal(
            ["上環街市(西港城)總站", "中環街市", "筲箕灣總站"],
            stops.Data.Select(item => item.NameZh));
        Assert.Equal([1, 2, 3], stops.Data.Select(item => item.Sequence));
    }

    [Fact]
    public async Task ReturnsAtMostTenUpcomingOriginDepartures()
    {
        var provider = CreateProvider(Schedule(), "2026-10-05T06:00:00+08:00");

        var result = await provider.GetDeparturesAsync("4001", 0, CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Equal("active", result.ServiceDayStatus);
        Assert.Equal(10, result.Data.NextDepartures.Count);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T06:02:00+08:00"), result.Data.NextDepartures[0]);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T05:42:00+08:00"), result.Data.FirstDepartureAt);
        Assert.Equal("起點班表，非本站即時到站時間。", result.Message);
    }

    [Fact]
    public async Task ReportsLastScheduledDepartureAfterServiceEndsAcrossMidnight()
    {
        var schedule = Schedule(
            [new("4001-1-MONDAY-2356", "23:56:00", "24:07:00", 600)],
            [
                new("4001-1-MONDAY-2356", 1, "23:56:00", "EAST"),
                new("4001-1-MONDAY-2356", 2, null, "CENTRAL"),
                new("4001-1-MONDAY-2356", 3, null, "WEST")
            ]);
        var provider = CreateProvider(schedule, "2026-10-06T01:00:00+08:00");

        var result = await provider.GetDeparturesAsync("4001", 0, CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.NextDepartures);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T00:06:00+08:00"), result.LastDepartureAt);
        Assert.Equal("本日已無車次，末班車已於 00:06 駛離起點站。", result.Message);
    }

    private static HongKongTramTransitProvider CreateProvider(
        HongKongGtfsSchedule schedule,
        string now) =>
        new(
            new StubScheduleProvider(schedule),
            new FixedTimeProvider(DateTimeOffset.Parse(now)));

    private static HongKongGtfsSchedule Schedule(
        IReadOnlyList<HongKongGtfsFrequency>? frequencies = null,
        IReadOnlyList<HongKongGtfsStopTime>? firstTripStopTimes = null)
    {
        var directionZero = firstTripStopTimes ??
        [
            new("4001-1-MONDAY-0542", 1, "05:42:00", "EAST"),
            new("4001-1-MONDAY-0542", 2, null, "CENTRAL"),
            new("4001-1-MONDAY-0542", 3, null, "WEST")
        ];
        return new HongKongGtfsSchedule(
            [new("4001", "TRAM", "", "筲箕灣 - 上環(西港城)(持有樂悠卡的60至64歲乘客車費兩元)")],
            [
                new(directionZero[0].TripId, "4001", "MONDAY", 0),
                new("4001-2-MONDAY-0640", "4001", "MONDAY", 0)
            ],
            frequencies ?? [new("4001-1-MONDAY-0542", "05:42:00", "08:00:00", 600)],
            [
                ..directionZero,
                new("4001-2-MONDAY-0640", 1, "06:40:00", "WEST"),
                new("4001-2-MONDAY-0640", 2, null, "CENTRAL"),
                new("4001-2-MONDAY-0640", 3, null, "EAST")
            ],
            [new("MONDAY", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 5), [DayOfWeek.Monday])],
            [],
            [
                new("EAST", "筲箕灣總站", 22.281, 114.229),
                new("CENTRAL", "中環街市", 22.284, 114.158),
                new("WEST", "上環街市(西港城)總站", 22.287, 114.150)
            ]);
    }

    private sealed class StubScheduleProvider(HongKongGtfsSchedule schedule)
        : IHongKongGtfsScheduleProvider
    {
        public Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderQueryResult<HongKongGtfsSchedule>(
                schedule,
                "scheduled",
                null,
                DateTimeOffset.Parse("2026-10-05T00:00:00Z"),
                false,
                null,
                "香港運輸署 GTFS"));

        public DateTimeOffset? FindLastOriginDeparture(
            string agencyId,
            string route,
            int direction,
            DateTimeOffset now,
            HongKongGtfsSchedule value,
            string? originName = null) => null;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
