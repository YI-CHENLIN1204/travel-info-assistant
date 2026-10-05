using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Providers.Nlb;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class NlbTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-05T23:00:00+08:00");

    [Fact]
    public async Task ReportsEndedServiceForTheSelectedVariantOrigin()
    {
        var schedule = new HongKongGtfsSchedule(
            [new("1723", "NLB", "1")],
            [
                new("mui-wo-trip", "1723", "MONDAY", 0),
                new("tai-o-trip", "1723", "MONDAY", 0)
            ],
            [],
            [
                new("mui-wo-trip", 1, "21:00:00", "MUI_WO"),
                new("tai-o-trip", 1, "23:30:00", "TAI_O")
            ],
            [new("MONDAY", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), [DayOfWeek.Monday])],
            [],
            [new("MUI_WO", "[NLB] 梅窩碼頭"), new("TAI_O", "[NLB] 大澳")]);
        var client = new StubNlbApiClient();
        var provider = new NlbTransitProvider(
            client,
            new StubScheduleProvider(schedule),
            new PassThroughProviderCache(),
            new FixedTimeProvider(Now),
            NullLogger<NlbTransitProvider>.Instance);

        var result = await provider.GetBusArrivalsAsync("1", 0, "1", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-05T21:00:00+08:00"), result.LastDepartureAt);
        Assert.Equal("官方每日班表", result.LastDepartureDescription);
    }

    private sealed class StubNlbApiClient : INlbApiClient
    {
        public Task<NlbHttpResult<IReadOnlyList<NlbRouteRow>>> GetRoutesAsync(
            CancellationToken cancellationToken) => Task.FromResult(
                new NlbHttpResult<IReadOnlyList<NlbRouteRow>>(
                    [new NlbRouteRow { RouteId = "1", RouteNumber = "1", NameZh = "梅窩碼頭 > 大澳" }],
                    Now));

        public Task<NlbHttpResult<IReadOnlyList<NlbStopRow>>> GetStopsAsync(
            string routeId,
            CancellationToken cancellationToken) => Task.FromResult(
                new NlbHttpResult<IReadOnlyList<NlbStopRow>>(
                    [new NlbStopRow { StopId = "1", NameZh = "梅窩碼頭" }],
                    Now));

        public Task<NlbHttpResult<IReadOnlyList<NlbEtaRow>>> GetEtaAsync(
            string routeId,
            string stopId,
            CancellationToken cancellationToken) => Task.FromResult(
                new NlbHttpResult<IReadOnlyList<NlbEtaRow>>([], Now));
    }

    private sealed class StubScheduleProvider(HongKongGtfsSchedule schedule) : IHongKongGtfsScheduleProvider
    {
        private readonly HongKongGtfsScheduleProvider _calculator = new(null!, null!, null!, TimeProvider.System);

        public Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(
            CancellationToken cancellationToken) => Task.FromResult(
                new ProviderQueryResult<HongKongGtfsSchedule>(
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
