using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.Citybus;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Providers.Kmb;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class HongKongBusTransitProviderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-05T12:00:00+08:00");

    [Fact]
    public async Task KeepsSameNumberRoutesDistinctAndDispatchesByQueryId()
    {
        var kmb = new StubKmbProvider { Routes = [Route("KMB:1", "九巴／龍運")] };
        var citybus = new StubCitybusProvider { Routes = [Route("CTB:1", "城巴")] };
        var provider = new HongKongBusTransitProvider(kmb, citybus, TimeProvider.System);

        var routes = await provider.GetBusRoutesAsync(CancellationToken.None);
        await provider.GetBusStopsAsync("CTB:1", 1, CancellationToken.None);
        await provider.GetBusArrivalsAsync("KMB:1", 0, "A", CancellationToken.None);

        Assert.Equal(2, routes.Data.Count);
        Assert.Equal(["CTB:1", "KMB:1"], routes.Data.Select(item => item.QueryId).Order());
        Assert.Equal([("1", 1)], citybus.StopRequests);
        Assert.Equal([("1", 0, "A")], kmb.ArrivalRequests);
    }

    [Fact]
    public async Task KeepsKmbRoutesWhenCitybusIsUnavailable()
    {
        var kmb = new StubKmbProvider { Routes = [Route("KMB:1", "九巴／龍運")] };
        var citybus = new StubCitybusProvider { Unavailable = true };
        var provider = new HongKongBusTransitProvider(kmb, citybus, TimeProvider.System);

        var result = await provider.GetBusRoutesAsync(CancellationToken.None);

        Assert.Single(result.Data);
        Assert.NotEqual("unavailable", result.DataStatus);
        Assert.Contains("城巴", result.Message);
    }

    private static TransitRouteResponse Route(string queryId, string operatorName) => new(
        queryId,
        "1",
        "1",
        "起點",
        "終點",
        [operatorName],
        [new TransitDirectionResponse(0, "終點", "起點", "終點")],
        [],
        queryId);

    private sealed class StubKmbProvider : IKmbTransitProvider
    {
        public IReadOnlyList<TransitRouteResponse> Routes { get; init; } = [];
        public List<(string Route, int Direction, string Stop)> ArrivalRequests { get; } = [];

        public Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
            CancellationToken cancellationToken) => Task.FromResult(Result(Routes, "九巴"));

        public Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
            string route,
            int direction,
            CancellationToken cancellationToken) => Task.FromResult(Result<IReadOnlyList<TransitStopResponse>>([], "九巴"));

        public Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
            string route,
            int direction,
            string stopId,
            CancellationToken cancellationToken)
        {
            ArrivalRequests.Add((route, direction, stopId));
            return Task.FromResult(Result<IReadOnlyList<TransitArrivalResponse>>([], "九巴"));
        }
    }

    private sealed class StubCitybusProvider : ICitybusTransitProvider
    {
        public IReadOnlyList<TransitRouteResponse> Routes { get; init; } = [];
        public bool Unavailable { get; init; }
        public List<(string Route, int Direction)> StopRequests { get; } = [];

        public Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
            CancellationToken cancellationToken) => Task.FromResult(
                Unavailable
                    ? ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>.Unavailable(
                        [], "目前無法連線至香港城巴開放數據。", TimeProvider.System, "城巴")
                    : Result(Routes, "城巴"));

        public Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
            string route,
            int direction,
            CancellationToken cancellationToken)
        {
            StopRequests.Add((route, direction));
            return Task.FromResult(Result<IReadOnlyList<TransitStopResponse>>([], "城巴"));
        }

        public Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
            string route,
            int direction,
            string stopId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<IReadOnlyList<TransitArrivalResponse>>([], "城巴"));
    }

    private static ProviderQueryResult<T> Result<T>(T data, string source) =>
        new(data, "scheduled", Now, Now, false, null, source);
}
