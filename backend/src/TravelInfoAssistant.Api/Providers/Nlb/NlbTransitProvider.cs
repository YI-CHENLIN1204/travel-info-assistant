using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Nlb;

public interface INlbTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeId,
        int direction,
        string stopId,
        CancellationToken cancellationToken);
}

public sealed class NlbTransitProvider(
    INlbApiClient apiClient,
    IHongKongGtfsScheduleProvider scheduleProvider,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<NlbTransitProvider> logger) : INlbTransitProvider
{
    private const string Source = "香港新大嶼山巴士開放數據";
    private static readonly TimeSpan NetworkFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan NetworkRetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan RealtimeFreshFor = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RealtimeRetainFor = TimeSpan.FromMinutes(2);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var routes = await GetRoutesAsync(cancellationToken);
            return Copy(routes, NlbTransitMapper.MapRoutes(routes.Data), "目前沒有新大嶼山巴士路線資料。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("route", exception);
            return Unavailable<TransitRouteResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken)
    {
        if (direction != 0) return Unavailable<TransitStopResponse>();
        try
        {
            var stops = await GetStopsAsync(routeId, cancellationToken);
            return Copy(stops, NlbTransitMapper.MapStops(stops.Data), "目前沒有這個方向的站序資料。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("stop", exception);
            return Unavailable<TransitStopResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeId,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        if (direction != 0) return Unavailable<TransitArrivalResponse>();
        try
        {
            var routes = await GetRoutesAsync(cancellationToken);
            var route = routes.Data.FirstOrDefault(item =>
                item.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase));
            if (route is null) return Unavailable<TransitArrivalResponse>();

            var stops = await GetStopsAsync(routeId, cancellationToken);
            var stop = stops.Data.FirstOrDefault(item =>
                item.StopId.Equals(stopId, StringComparison.OrdinalIgnoreCase));
            if (stop is null) return Unavailable<TransitArrivalResponse>();

            var eta = await GetEtaAsync(routeId, stopId, cancellationToken);
            var (_, destinationName) = NlbTransitMapper.Endpoints(route.NameZh);
            var mapped = NlbTransitMapper.MapArrivals(
                routeId,
                route.RouteNumber,
                destinationName,
                stopId,
                stop.NameZh,
                eta.FetchedAt,
                eta.Data);
            if (mapped.Count > 0) return Copy(eta, mapped, null, "realtime");

            if (!eta.Stale && stops.Data.Count > 0)
            {
                var schedule = await scheduleProvider.GetScheduleAsync(cancellationToken);
                var lastDeparture = scheduleProvider.FindLastOriginDeparture(
                    "NLB",
                    route.RouteNumber,
                    0,
                    timeProvider.GetUtcNow(),
                    schedule.Data,
                    stops.Data[0].NameZh);
                if (lastDeparture.HasValue && lastDeparture.Value < timeProvider.GetUtcNow())
                {
                    return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                        [],
                        "scheduled",
                        schedule.SourceUpdatedAt,
                        eta.FetchedAt,
                        schedule.Stale,
                        schedule.Message,
                        Source,
                        "ended",
                        lastDeparture,
                        "官方每日班表");
                }
            }

            return Copy(eta, mapped, "目前沒有這一站的到站預報。", "realtime");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("arrival", exception);
            return Unavailable<TransitArrivalResponse>();
        }
    }

    private Task<ProviderQueryResult<IReadOnlyList<NlbRouteRow>>> GetRoutesAsync(CancellationToken token) =>
        Cache("transit:nlb:routes:v1", NetworkFreshFor, NetworkRetainFor, apiClient.GetRoutesAsync, token);

    private Task<ProviderQueryResult<IReadOnlyList<NlbStopRow>>> GetStopsAsync(
        string routeId,
        CancellationToken token) =>
        Cache($"transit:nlb:stops:{routeId}:v1", NetworkFreshFor, NetworkRetainFor,
            ct => apiClient.GetStopsAsync(routeId, ct), token);

    private Task<ProviderQueryResult<IReadOnlyList<NlbEtaRow>>> GetEtaAsync(
        string routeId,
        string stopId,
        CancellationToken token) =>
        Cache($"transit:nlb:eta:{routeId}:{stopId}:v1", RealtimeFreshFor, RealtimeRetainFor,
            ct => apiClient.GetEtaAsync(routeId, stopId, ct), token, "realtime");

    private async Task<ProviderQueryResult<T>> Cache<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<NlbHttpResult<T>>> factory,
        CancellationToken token,
        string status = "scheduled")
    {
        var result = await cache.GetOrCreateAsync(
            key,
            freshFor,
            retainFor,
            async cancellationToken =>
            {
                var response = await factory(cancellationToken);
                return new ProviderPayload<T>(response.Data, status, null, response.FetchedAt);
            },
            token);
        return result with { Source = Source };
    }

    private static ProviderQueryResult<IReadOnlyList<TOutput>> Copy<TSource, TOutput>(
        ProviderQueryResult<TSource> source,
        IReadOnlyList<TOutput> data,
        string? emptyMessage,
        string status = "scheduled") =>
        new(
            data,
            source.DataStatus == "cached" ? "cached" : status,
            source.SourceUpdatedAt,
            source.FetchedAt,
            source.Stale,
            source.Message ?? (data.Count == 0 ? emptyMessage : null),
            Source);

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>() =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            [],
            "新大嶼山巴士資料目前暫時無法取得，請稍後再試。",
            timeProvider,
            Source);

    private void LogFailure(string operation, Exception exception) =>
        logger.LogWarning(exception, "NLB {Operation} query failed ({ErrorType}).", operation, exception.GetType().Name);
}
