using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Kmb;

public interface IKmbTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string route,
        int direction,
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string route,
        int direction,
        string stopId,
        CancellationToken cancellationToken);
}

public sealed class KmbTransitProvider(
    IKmbApiClient apiClient,
    IHongKongGtfsScheduleProvider scheduleProvider,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<KmbTransitProvider> logger) : IKmbTransitProvider
{
    private const string Source = "香港九巴／龍運開放數據";
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
            return Copy(routes, KmbTransitMapper.MapRoutes(routes.Data), "目前查無九巴／龍運一般路線。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("route", exception);
            return Unavailable<TransitRouteResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string route,
        int direction,
        CancellationToken cancellationToken)
    {
        try
        {
            var routeStops = await GetRouteStopsAsync(cancellationToken);
            var stops = await GetStopsAsync(cancellationToken);
            var mapped = KmbTransitMapper.MapStops(route, direction, routeStops.Data, stops.Data);
            return new ProviderQueryResult<IReadOnlyList<TransitStopResponse>>(
                mapped,
                routeStops.DataStatus == "cached" || stops.DataStatus == "cached" ? "cached" : "scheduled",
                Latest(routeStops.SourceUpdatedAt, stops.SourceUpdatedAt),
                Latest(routeStops.FetchedAt, stops.FetchedAt),
                routeStops.Stale || stops.Stale,
                routeStops.Message ?? stops.Message ?? (mapped.Count == 0 ? "目前查無此方向的站序。" : null),
                Source);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("stop", exception);
            return Unavailable<TransitStopResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string route,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        try
        {
            var eta = await GetEtaAsync(stopId, route, cancellationToken);
            var stops = await GetStopsAsync(cancellationToken);
            var stopName = stops.Data.FirstOrDefault(item =>
                item.StopId.Equals(stopId, StringComparison.OrdinalIgnoreCase))?.NameZh ?? stopId;
            var mapped = KmbTransitMapper.MapArrivals(route, direction, stopId, stopName, eta.Data);
            if (mapped.Count > 0)
            {
                return Copy(eta, mapped, null, "realtime");
            }

            if (!eta.Stale)
            {
                var schedule = await scheduleProvider.GetScheduleAsync(cancellationToken);
                var lastDeparture = scheduleProvider.FindLastOriginDeparture(
                    "KMB",
                    route,
                    direction,
                    timeProvider.GetUtcNow(),
                    schedule.Data);
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
                        "由起點開出");
                }
            }

            return Copy(eta, mapped, "目前查無接下來的到站預報。", "realtime");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("arrival", exception);
            return Unavailable<TransitArrivalResponse>();
        }
    }

    private Task<ProviderQueryResult<IReadOnlyList<KmbRouteRow>>> GetRoutesAsync(CancellationToken token) =>
        Cache("transit:kmb:routes:v1", NetworkFreshFor, NetworkRetainFor, apiClient.GetRoutesAsync, token);
    private Task<ProviderQueryResult<IReadOnlyList<KmbRouteStopRow>>> GetRouteStopsAsync(CancellationToken token) =>
        Cache("transit:kmb:route-stops:v1", NetworkFreshFor, NetworkRetainFor, apiClient.GetRouteStopsAsync, token);
    private Task<ProviderQueryResult<IReadOnlyList<KmbStopRow>>> GetStopsAsync(CancellationToken token) =>
        Cache("transit:kmb:stops:v1", NetworkFreshFor, NetworkRetainFor, apiClient.GetStopsAsync, token);
    private Task<ProviderQueryResult<IReadOnlyList<KmbEtaRow>>> GetEtaAsync(string stopId, string route, CancellationToken token) =>
        Cache($"transit:kmb:eta:{stopId}:{route}:v1", RealtimeFreshFor, RealtimeRetainFor,
            ct => apiClient.GetEtaAsync(stopId, route, ct), token, "realtime");
    private async Task<ProviderQueryResult<T>> Cache<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<KmbHttpResult<T>>> factory,
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
                return new ProviderPayload<T>(response.Data, status, response.SourceUpdatedAt, response.FetchedAt);
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
            "目前無法連線至香港九巴／龍運開放數據，請稍後再試。",
            timeProvider,
            Source);

    private void LogFailure(string operation, Exception exception) =>
        logger.LogWarning(exception, "KMB {Operation} query failed ({ErrorType}).", operation, exception.GetType().Name);

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second) =>
        first >= second ? first : second;
    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first.HasValue && second.HasValue ? Latest(first.Value, second.Value) : first ?? second;
}
