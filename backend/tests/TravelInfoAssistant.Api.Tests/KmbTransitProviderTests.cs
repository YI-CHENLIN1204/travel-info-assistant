using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Providers.Kmb;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class KmbTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-05T01:00:00+08:00");

    [Fact]
    public async Task ReportsEndedServiceFromGtfsOriginScheduleWhenEtaIsEmpty()
    {
        var client = new StubKmbApiClient
        {
            Stops = [new KmbStopRow { StopId = "A", NameZh = "中秀茂坪" }],
            Schedule = new HongKongGtfsSchedule(
                [new("1053", "KMB", "1A")],
                [new("trip-1", "1053", "SUNDAY", 0)],
                [new("trip-1", "23:55:00", "25:00:00", 600)],
                [new("trip-1", 1, "23:55:00")],
                [new("SUNDAY", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), [DayOfWeek.Sunday])],
                [])
        };
        var provider = new KmbTransitProvider(
            client,
            new StubScheduleProvider(client.Schedule),
            new PassThroughProviderCache(),
            new FixedTimeProvider(Now),
            NullLogger<KmbTransitProvider>.Instance);

        var result = await provider.GetBusArrivalsAsync("1A", 0, "A", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T00:55:00+08:00"), result.LastDepartureAt);
        Assert.Equal("由起點開出", result.LastDepartureDescription);
    }

    private sealed class StubKmbApiClient : IKmbApiClient
    {
        public IReadOnlyList<KmbStopRow> Stops { get; init; } = [];
        public HongKongGtfsSchedule Schedule { get; init; } = new([], [], [], [], [], []);

        public Task<KmbHttpResult<IReadOnlyList<KmbRouteRow>>> GetRoutesAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new KmbHttpResult<IReadOnlyList<KmbRouteRow>>([], Now));

        public Task<KmbHttpResult<IReadOnlyList<KmbRouteStopRow>>> GetRouteStopsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new KmbHttpResult<IReadOnlyList<KmbRouteStopRow>>([], Now));

        public Task<KmbHttpResult<IReadOnlyList<KmbStopRow>>> GetStopsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new KmbHttpResult<IReadOnlyList<KmbStopRow>>(Stops, Now));

        public Task<KmbHttpResult<IReadOnlyList<KmbEtaRow>>> GetEtaAsync(
            string stopId,
            string route,
            CancellationToken cancellationToken) =>
            Task.FromResult(new KmbHttpResult<IReadOnlyList<KmbEtaRow>>([], Now, Now));

    }

    private sealed class StubScheduleProvider(HongKongGtfsSchedule schedule) : IHongKongGtfsScheduleProvider
    {
        private readonly HongKongGtfsScheduleProvider _calculator = new(null!, null!, null!, TimeProvider.System);

        public Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderQueryResult<HongKongGtfsSchedule>(
                schedule,
                "scheduled",
                null,
                Now,
                false,
                null,
                "香港運輸署 GTFS"));

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
