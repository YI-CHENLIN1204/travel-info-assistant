using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.Citybus;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class CitybusTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-05T01:00:00+08:00");

    [Fact]
    public async Task ReportsEndedServiceFromSharedGtfsWhenEtaIsEmpty()
    {
        var schedule = new HongKongGtfsSchedule(
            [new("2001", "CTB", "1")],
            [new("trip-1", "2001", "SUNDAY", 0)],
            [new("trip-1", "23:55:00", "25:00:00", 600)],
            [new("trip-1", 1, "23:55:00")],
            [new("SUNDAY", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), [DayOfWeek.Sunday])],
            []);
        var client = new StubCitybusApiClient
        {
            Stops = [new CitybusStopRow { StopId = "A", NameZh = "跑馬地" }]
        };
        var provider = new CitybusTransitProvider(
            client,
            new StubScheduleProvider(schedule),
            new PassThroughProviderCache(),
            new FixedTimeProvider(Now),
            NullLogger<CitybusTransitProvider>.Instance);

        var result = await provider.GetBusArrivalsAsync("1", 0, "A", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T00:55:00+08:00"), result.LastDepartureAt);
        Assert.Equal("由起點開出", result.LastDepartureDescription);
    }

    private sealed class StubCitybusApiClient : ICitybusApiClient
    {
        public IReadOnlyList<CitybusStopRow> Stops { get; init; } = [];

        public Task<CitybusHttpResult<IReadOnlyList<CitybusRouteRow>>> GetRoutesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CitybusHttpResult<IReadOnlyList<CitybusRouteRow>>([], Now));

        public Task<CitybusHttpResult<IReadOnlyList<CitybusRouteStopRow>>> GetRouteStopsAsync(
            string route,
            int direction,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CitybusHttpResult<IReadOnlyList<CitybusRouteStopRow>>([], Now));

        public Task<CitybusHttpResult<CitybusStopRow>> GetStopAsync(
            string stopId,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CitybusHttpResult<CitybusStopRow>(
                Stops.Single(item => item.StopId == stopId),
                Now));

        public Task<CitybusHttpResult<IReadOnlyList<CitybusEtaRow>>> GetEtaAsync(
            string stopId,
            string route,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CitybusHttpResult<IReadOnlyList<CitybusEtaRow>>([], Now, Now));
    }

    private sealed class StubScheduleProvider(HongKongGtfsSchedule schedule) : IHongKongBusScheduleProvider
    {
        private readonly HongKongBusScheduleProvider _calculator = new(null!, null!, null!, TimeProvider.System);

        public Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderQueryResult<HongKongGtfsSchedule>(
                schedule, "scheduled", null, Now, false, null, "香港運輸署 GTFS"));

        public DateTimeOffset? FindLastOriginDeparture(
            string agencyId,
            string route,
            int direction,
            DateTimeOffset now,
            HongKongGtfsSchedule value,
            string? originName = null) =>
            _calculator.FindLastOriginDeparture(agencyId, route, direction, now, value, originName);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class PassThroughProviderCache : IProviderCache
    {
        public async Task<ProviderQueryResult<T>> GetOrCreateAsync<T>(
            string key,
            TimeSpan freshFor,
            TimeSpan retainFor,
            Func<CancellationToken, Task<ProviderPayload<T>>> factory,
            CancellationToken cancellationToken)
        {
            var payload = await factory(cancellationToken);
            return new ProviderQueryResult<T>(
                payload.Data,
                payload.DataStatus,
                payload.SourceUpdatedAt,
                payload.FetchedAt,
                false,
                null);
        }
    }
}
