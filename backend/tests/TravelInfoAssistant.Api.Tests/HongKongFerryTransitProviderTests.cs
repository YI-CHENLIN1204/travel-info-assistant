using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class HongKongFerryTransitProviderTests
{
    [Fact]
    public async Task MapsOnlyPublishedDirectionsAndPreservesPierOrder()
    {
        var provider = CreateProvider(Schedule(), "2026-10-06T11:00:00+08:00");

        var routes = await provider.GetRoutesAsync(CancellationToken.None);
        var forwardStops = await provider.GetStopsAsync("7005", 0, CancellationToken.None);
        var oneWayRoute = Assert.Single(routes.Data, item => item.Id == "7001");

        var route = Assert.Single(routes.Data, item => item.Id == "7005");
        Assert.Equal("中環 - 長洲", route.NameZh);
        Assert.Equal(2, route.Directions.Count);
        Assert.Equal("中環五號碼頭", route.Directions[0].OriginName);
        Assert.Equal("長洲碼頭", route.Directions[0].DestinationName);
        Assert.Equal(["中環五號碼頭", "長洲碼頭"], forwardStops.Data.Select(item => item.NameZh));
        Assert.Single(oneWayRoute.Directions);
        Assert.Equal("香港仔碼頭", oneWayRoute.Directions[0].OriginName);
    }

    [Fact]
    public async Task ReturnsUpcomingDeparturesWithScheduledDestinationArrival()
    {
        var provider = CreateProvider(Schedule(), "2026-10-06T11:00:00+08:00");

        var result = await provider.GetJourneysAsync("7005", 0, CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Equal("not-started", result.ServiceDayStatus);
        Assert.Collection(
            result.Data.NextJourneys,
            first =>
            {
                Assert.Equal(DateTimeOffset.Parse("2026-10-06T12:00:00+08:00"), first.DepartureAt);
                Assert.Equal(DateTimeOffset.Parse("2026-10-06T13:00:00+08:00"), first.ArrivalAt);
            },
            second =>
            {
                Assert.Equal(DateTimeOffset.Parse("2026-10-06T14:00:00+08:00"), second.DepartureAt);
                Assert.Equal(DateTimeOffset.Parse("2026-10-06T15:00:00+08:00"), second.ArrivalAt);
            });
    }

    [Fact]
    public async Task ReportsScheduledLastSailingAcrossMidnight()
    {
        var schedule = Schedule(
            serviceId: "MONDAY",
            serviceDate: new DateOnly(2026, 10, 5),
            forwardTrips:
            [
                ("7005-1-MONDAY-2430", "24:30:00", "25:30:00")
            ]);
        var provider = CreateProvider(schedule, "2026-10-06T01:45:00+08:00");

        var result = await provider.GetJourneysAsync("7005", 0, CancellationToken.None);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.NextJourneys);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-06T00:30:00+08:00"), result.LastDepartureAt);
        Assert.Equal("本日已無船班，末班船已於 00:30 駛離碼頭。", result.Message);
    }

    private static HongKongFerryTransitProvider CreateProvider(
        HongKongGtfsSchedule schedule,
        string now) =>
        new(
            new StubScheduleProvider(schedule),
            new FixedTimeProvider(DateTimeOffset.Parse(now)));

    private static HongKongGtfsSchedule Schedule(
        string serviceId = "TUESDAY",
        DateOnly? serviceDate = null,
        IReadOnlyList<(string Id, string Departure, string Arrival)>? forwardTrips = null)
    {
        var date = serviceDate ?? new DateOnly(2026, 10, 6);
        var forward = forwardTrips ??
        [
            ("7005-1-TUESDAY-1200", "12:00:00", "13:00:00"),
            ("7005-1-TUESDAY-1400", "14:00:00", "15:00:00")
        ];
        var trips = forward
            .Select(item => new HongKongGtfsTrip(item.Id, "7005", serviceId, 0))
            .Append(new HongKongGtfsTrip("7005-2-TUESDAY-1230", "7005", serviceId, 0))
            .Append(new HongKongGtfsTrip("7001-1-TUESDAY-1800", "7001", serviceId, 0))
            .ToList();
        var stopTimes = forward.SelectMany(item => new[]
            {
                new HongKongGtfsStopTime(item.Id, 1, item.Departure, "CENTRAL", item.Departure),
                new HongKongGtfsStopTime(item.Id, 2, item.Arrival, "CHEUNG_CHAU", item.Arrival)
            })
            .Concat(
            [
                new("7005-2-TUESDAY-1230", 1, "12:30:00", "CHEUNG_CHAU", "12:30:00"),
                new("7005-2-TUESDAY-1230", 2, "13:30:00", "CENTRAL", "13:30:00"),
                new("7001-1-TUESDAY-1800", 1, "18:00:00", "ABERDEEN", "18:00:00"),
                new("7001-1-TUESDAY-1800", 2, null, "PO_TOI", "18:36:00")
            ])
            .ToList();

        return new HongKongGtfsSchedule(
            [
                new("7005", "FERRY", "", "中環 - 長洲"),
                new("7001", "FERRY", "", "蒲台島 - 香港仔(單程成人收費$50)")
            ],
            trips,
            [],
            stopTimes,
            [new(serviceId, date, date, [date.DayOfWeek])],
            [],
            [
                new("CENTRAL", "中環五號碼頭", 22.288, 114.159),
                new("CHEUNG_CHAU", "長洲碼頭", 22.209, 114.028),
                new("ABERDEEN", "香港仔碼頭", 22.248, 114.155),
                new("PO_TOI", "蒲台島碼頭", 22.167, 114.263)
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
                DateTimeOffset.Parse("2026-10-06T00:00:00Z"),
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
