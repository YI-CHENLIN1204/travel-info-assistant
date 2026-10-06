using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.LtaDataMall;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaDataMallBusTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T12:00:00+08:00");

    [Fact]
    public async Task ProvidesRouteStopsAndTopThreeArrivals()
    {
        var client = new StubClient
        {
            Arrival = new LtaBusArrivalResponse
            {
                BusStopCode = "01012",
                Services =
                [
                    new LtaBusArrivalService
                    {
                        ServiceNo = "36",
                        Operator = "SBST",
                        NextBus = Estimate(3),
                        NextBus2 = Estimate(8),
                        NextBus3 = Estimate(15)
                    }
                ]
            }
        };
        var provider = CreateProvider(client, Now);

        var routes = await provider.GetBusRoutesAsync(CancellationToken.None);
        var route = Assert.Single(routes.Data);
        var stops = await provider.GetBusStopsAsync(route.QueryId!, 0, CancellationToken.None);
        var arrivals = await provider.GetBusArrivalsAsync(
            route.QueryId!,
            0,
            "01012",
            CancellationToken.None);

        Assert.Equal("新加坡 LTA DataMall", routes.Source);
        Assert.Equal(3, stops.Data.Count);
        Assert.Equal(3, arrivals.Data.Count);
        Assert.Equal("realtime", arrivals.DataStatus);
    }

    [Fact]
    public async Task ReportsOfficialLastBusWhenNoArrivalRemains()
    {
        var provider = CreateProvider(
            new StubClient(),
            DateTimeOffset.Parse("2026-10-01T23:40:00+08:00"));

        var result = await provider.GetBusArrivalsAsync(
            "LTA-BUS:SBST:36",
            0,
            "01012",
            CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T23:30:00+08:00"), result.LastDepartureAt);
        Assert.Equal("駛離站", result.LastDepartureDescription);
    }

    private static LtaDataMallBusTransitProvider CreateProvider(
        StubClient client,
        DateTimeOffset now) =>
        new(
            client,
            new PassThroughProviderCache(),
            new FixedTimeProvider(now),
            NullLogger<LtaDataMallBusTransitProvider>.Instance);

    private static LtaBusArrivalEstimate Estimate(int minutes) =>
        new()
        {
            DestinationCode = "03000",
            EstimatedArrival = Now.AddMinutes(minutes).ToString("O"),
            Monitored = 1
        };

    private sealed class StubClient : ILtaDataMallBusApiClient
    {
        private readonly LtaBusNetwork _network = LtaDataMallBusTransitMapperTests.CreateNetwork();
        public LtaBusArrivalResponse Arrival { get; init; } = new();

        public Task<LtaHttpResult<IReadOnlyList<LtaBusServiceRow>>> GetBusServicesAsync(
            CancellationToken cancellationToken) => Result(_network.Services);

        public Task<LtaHttpResult<IReadOnlyList<LtaBusRouteRow>>> GetBusRoutesAsync(
            CancellationToken cancellationToken) => Result(_network.Routes);

        public Task<LtaHttpResult<IReadOnlyList<LtaBusStopRow>>> GetBusStopsAsync(
            CancellationToken cancellationToken) => Result(_network.Stops);

        public Task<LtaHttpResult<LtaBusArrivalResponse>> GetBusArrivalsAsync(
            string busStopCode,
            string serviceNo,
            CancellationToken cancellationToken) => Result(Arrival);

        private static Task<LtaHttpResult<T>> Result<T>(T value) =>
            Task.FromResult(new LtaHttpResult<T>(value, Now, Now));
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
