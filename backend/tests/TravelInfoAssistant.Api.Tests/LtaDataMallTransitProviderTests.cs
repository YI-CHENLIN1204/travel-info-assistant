using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.LtaDataMall;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class LtaDataMallTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T04:00:00Z");

    [Fact]
    public async Task ProvidesRouteStationArrivalAndAlertCards()
    {
        var client = new StubClient
        {
            Network = CreateNetwork(),
            TripFeed = new LtaRealtimeFeed(
                Now,
                [new LtaTripUpdate(
                    "prediction",
                    "trip-1",
                    "NS",
                    0,
                    0,
                    Now,
                    [new LtaStopTimeUpdate(
                        "NS22",
                        1,
                        Now.AddMinutes(3),
                        Now.AddMinutes(3),
                        60,
                        60,
                        0)])],
                []),
            AlertFeed = new LtaRealtimeFeed(
                Now,
                [],
                [new LtaServiceAlert(
                    "alert",
                    ["NS"],
                    [],
                    Now.AddMinutes(-5),
                    Now.AddMinutes(20),
                    "Train delay",
                    null,
                    3)])
        };
        var provider = CreateProvider(client);

        var routes = await provider.GetMetroRoutesAsync(CancellationToken.None);
        var stations = await provider.GetMetroStationsAsync(CancellationToken.None);
        var arrivals = await provider.GetMetroArrivalsAsync(
            "LTA:NS:NS22",
            CancellationToken.None);
        var status = await provider.GetMetroStatusAsync(
            "LTA:NS",
            CancellationToken.None);

        Assert.Equal("新加坡 LTA DataMall", routes.Source);
        Assert.Equal("LTA:NS", Assert.Single(routes.Data).Id);
        Assert.Equal("烏節", Assert.Single(stations.Data).NameZh);
        Assert.Equal("realtime", arrivals.DataStatus);
        Assert.Equal("延誤 1 分鐘", Assert.Single(arrivals.Data).ServiceStatus);
        Assert.Contains("Train delay", Assert.Single(status.Data).MessageEn);
    }

    [Fact]
    public async Task FallsBackToScheduleWhenRealtimeFeedIsOlderThanTwoMinutes()
    {
        var oldTimestamp = Now.AddMinutes(-3);
        var client = new StubClient
        {
            Network = CreateNetwork(),
            TripFeed = new LtaRealtimeFeed(oldTimestamp, [], [])
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroArrivalsAsync(
            "LTA:NS:NS22",
            CancellationToken.None);

        Assert.Equal("scheduled", result.DataStatus);
        var arrival = Assert.Single(result.Data);
        Assert.Equal("表定時間", arrival.ServiceStatus);
        Assert.Null(arrival.EstimatedAt);
        Assert.False(result.Stale);
    }

    [Fact]
    public async Task ReportsScheduledLastDepartureAfterServiceEnds()
    {
        var now = DateTimeOffset.Parse("2026-10-01T15:00:00Z");
        var client = new StubClient
        {
            Network = CreateNetwork() with
            {
                StopTimes =
                [
                    new LtaGtfsStopTime(
                        "trip-1",
                        "NS22",
                        1,
                        "22:40:00",
                        "22:43:00")
                ]
            },
            TripFeed = new LtaRealtimeFeed(now, [], [])
        };
        var provider = CreateProvider(client, now);

        var result = await provider.GetMetroArrivalsAsync(
            "LTA:NS:NS22",
            CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T14:43:00Z"), result.LastDepartureAt);
    }

    private static LtaDataMallTransitProvider CreateProvider(
        StubClient client,
        DateTimeOffset? now = null) =>
        new(
            client,
            new PassThroughProviderCache(),
            new FixedTimeProvider(now ?? Now),
            NullLogger<LtaDataMallTransitProvider>.Instance);

    private static LtaGtfsNetwork CreateNetwork() =>
        new(
            [new LtaGtfsRoute("NS", "NS", "North South Line", "SMRT")],
            [new LtaGtfsStop("NS22", "NS22", "Orchard", null, 1, null, 1.304, 103.832)],
            [new LtaGtfsTrip("trip-1", "NS", "Marina South Pier", 0, "weekday")],
            [new LtaGtfsRouteStop("NS", "NS22", 0, 1)],
            [new LtaChineseStationName("NS22", "Orchard", "烏節", "North South Line", "南北線")],
            [new LtaGtfsStopTime("trip-1", "NS22", 1, "12:03:00", "12:03:00")],
            [new LtaGtfsCalendar(
                "weekday",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                [
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                ])],
            []);

    private sealed class StubClient : ILtaDataMallApiClient
    {
        public LtaGtfsNetwork Network { get; init; } = CreateNetwork();
        public LtaRealtimeFeed TripFeed { get; init; } = new(Now, [], []);
        public LtaRealtimeFeed AlertFeed { get; init; } = new(Now, [], []);

        public Task<LtaHttpResult<LtaGtfsNetwork>> GetNetworkAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new LtaHttpResult<LtaGtfsNetwork>(Network, Now, Now));

        public Task<LtaHttpResult<LtaRealtimeFeed>> GetTripUpdatesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new LtaHttpResult<LtaRealtimeFeed>(
                TripFeed,
                Now,
                TripFeed.Timestamp));

        public Task<LtaHttpResult<LtaRealtimeFeed>> GetServiceAlertsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new LtaHttpResult<LtaRealtimeFeed>(
                AlertFeed,
                Now,
                AlertFeed.Timestamp));
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
