using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.Tdx;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class TdxTransitProviderTests
{
    [Fact]
    public async Task GetBusRoutesAsync_CorrectsDuplicatedOutboundEndpointsForReturnDirection()
    {
        IReadOnlyList<TdxBusRoute> routes =
        [
            new TdxBusRoute
            {
                RouteUID = "TPE214",
                RouteID = "214",
                RouteName = Name("214"),
                DepartureStopNameZh = "中和",
                DestinationStopNameZh = "內湖",
                SubRoutes =
                [
                    new TdxBusSubRoute
                    {
                        Direction = 0,
                        SubRouteName = Name("214"),
                        DepartureStopNameZh = "中和",
                        DestinationStopNameZh = "內湖"
                    },
                    new TdxBusSubRoute
                    {
                        Direction = 1,
                        SubRouteName = Name("214"),
                        DepartureStopNameZh = "中和",
                        DestinationStopNameZh = "內湖"
                    }
                ]
            }
        ];
        var provider = CreateProvider([], [], routes);

        var result = await provider.GetBusRoutesAsync(CancellationToken.None);

        var route = Assert.Single(result.Data);
        Assert.Collection(
            route.Directions,
            outbound =>
            {
                Assert.Equal(0, outbound.Direction);
                Assert.Equal("中和", outbound.OriginName);
                Assert.Equal("內湖", outbound.DestinationName);
            },
            inbound =>
            {
                Assert.Equal(1, inbound.Direction);
                Assert.Equal("內湖", inbound.OriginName);
                Assert.Equal("中和", inbound.DestinationName);
            });
    }

    [Fact]
    public async Task GetBusStopsAsync_DeduplicatesSharedSubRouteStops()
    {
        var provider = CreateProvider(CreateStopFeed(), []);

        var result = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);

        Assert.Collection(
            result.Data,
            stop =>
            {
                Assert.Equal("TPE1001", stop.Id);
                Assert.Equal("龍川中心", stop.NameZh);
                Assert.Equal(1, stop.Sequence);
            },
            stop =>
            {
                Assert.Equal("TPE1002", stop.Id);
                Assert.Equal("中和高中", stop.NameZh);
                Assert.Equal(2, stop.Sequence);
            });
    }

    [Fact]
    public async Task GetBusArrivalsAsync_MatchesEquivalentStopIdFromAnotherSubRoute()
    {
        IReadOnlyList<TdxBusArrival> arrivals =
        [
            Arrival("TPE1001", "1001"),
            // TDX can repeat the same vehicle under the equivalent stop ID of another sub-route.
            // The provider must still return one user-facing arrival.
            Arrival("NWT9001", "9001")
        ];
        var provider = CreateProvider(CreateStopFeed(), arrivals);

        var result = await provider.GetBusArrivalsAsync(
            "214",
            0,
            "TPE1001",
            CancellationToken.None);

        var arrival = Assert.Single(result.Data);
        Assert.Equal("TPE1001", arrival.StopId);
        Assert.Equal("龍川中心", arrival.StopName);
        Assert.Equal("214", arrival.RouteName);
    }

    private static TdxBusArrival Arrival(string stopUid, string stopId) =>
        new()
        {
            PlateNumb = "ABC-123",
            StopUID = stopUid,
            StopID = stopId,
            StopName = Name("龍川中心"),
            RouteUID = "TPE214",
            RouteID = "214",
            RouteName = Name("214"),
            Direction = 0,
            EstimateTime = 300,
            StopStatus = 0,
            SrcUpdateTime = DateTimeOffset.Parse("2026-09-22T21:00:00+08:00")
        };

    [Fact]
    public async Task GetBusStopsAsync_KeepsDifferentBranchStopsAtSameSequence()
    {
        IReadOnlyList<TdxBusStopOfRoute> stops =
        [
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops = [Stop("TPE3001", "3001", "支線甲", 3)]
            },
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops = [Stop("TPE3002", "3002", "支線乙", 3)]
            }
        ];
        var provider = CreateProvider(stops, []);

        var result = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);

        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, stop => stop.NameZh == "支線甲");
        Assert.Contains(result.Data, stop => stop.NameZh == "支線乙");
    }

    [Fact]
    public async Task GetBusStopsAsync_ReturnsOnlyTheRequestedDirection()
    {
        IReadOnlyList<TdxBusStopOfRoute> stops =
        [
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops =
                [
                    Stop("TPE4001", "4001", "中和", 1),
                    Stop("TPE4002", "4002", "內湖", 2)
                ]
            },
            new TdxBusStopOfRoute
            {
                Direction = 1,
                Stops =
                [
                    Stop("TPE5001", "5001", "內湖", 1),
                    Stop("TPE5002", "5002", "中和", 2)
                ]
            }
        ];
        var provider = CreateProvider(stops, []);

        var outbound = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);
        var inbound = await provider.GetBusStopsAsync("214", 1, CancellationToken.None);

        Assert.Equal(["中和", "內湖"], outbound.Data.Select(item => item.NameZh));
        Assert.Equal(["內湖", "中和"], inbound.Data.Select(item => item.NameZh));
    }

    private static TdxTransitProvider CreateProvider(
        IReadOnlyList<TdxBusStopOfRoute> stops,
        IReadOnlyList<TdxBusArrival> arrivals,
        IReadOnlyList<TdxBusRoute>? routes = null)
    {
        var apiClient = new StubTdxApiClient(new Dictionary<string, object>
        {
            ["v2/Bus/Route/City/Taipei"] = routes ?? Array.Empty<TdxBusRoute>(),
            ["v2/Bus/StopOfRoute/City/Taipei/214"] = stops,
            ["v2/Bus/EstimatedTimeOfArrival/City/Taipei/214"] = arrivals
        });
        return new TdxTransitProvider(
            apiClient,
            new PassThroughProviderCache(),
            TimeProvider.System,
            NullLogger<TdxTransitProvider>.Instance);
    }

    private static IReadOnlyList<TdxBusStopOfRoute> CreateStopFeed() =>
    [
        new TdxBusStopOfRoute
        {
            Direction = 0,
            Stops =
            [
                Stop("TPE1001", "1001", "龍川中心", 1),
                Stop("TPE1002", "1002", "中和高中", 2)
            ]
        },
        new TdxBusStopOfRoute
        {
            Direction = 0,
            Stops =
            [
                Stop("NWT9001", "9001", "龍川中心", 1),
                Stop("NWT9002", "9002", "中和高中", 2)
            ]
        }
    ];

    private static TdxBusStop Stop(
        string stopUid,
        string stopId,
        string name,
        int sequence) =>
        new()
        {
            StopUID = stopUid,
            StopID = stopId,
            StopName = Name(name),
            StopSequence = sequence
        };

    private static TdxLocalizedName Name(string value) => new() { ZhTw = value };

    private sealed class StubTdxApiClient(IReadOnlyDictionary<string, object> responses)
        : ITdxApiClient
    {
        public Task<TdxHttpResult<T>> GetAsync<T>(
            string relativePath,
            IReadOnlyDictionary<string, string?> query,
            CancellationToken cancellationToken)
        {
            if (!responses.TryGetValue(relativePath, out var value) || value is not T typed)
            {
                throw new InvalidOperationException($"No stub response for {relativePath}.");
            }

            return Task.FromResult(new TdxHttpResult<T>(
                typed,
                null,
                DateTimeOffset.Parse("2026-09-22T13:00:00Z"),
                0));
        }
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
