using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.HongKong;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Citybus;

public interface ICitybusTransitProvider
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

public sealed class CitybusTransitProvider(
    ICitybusApiClient apiClient,
    IHongKongGtfsScheduleProvider scheduleProvider,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<CitybusTransitProvider> logger) : ICitybusTransitProvider
{
    private const string Source = "香港城巴開放數據";
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
            return Copy(routes, CitybusTransitMapper.MapRoutes(routes.Data), "目前查無城巴路線。");
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
            var routeStops = await GetRouteStopsAsync(route, direction, cancellationToken);
            var stopResults = await Task.WhenAll(routeStops.Data
                .Select(item => item.StopId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(item => GetStopAsync(item, cancellationToken)));
            var mapped = CitybusTransitMapper.MapStops(
                route,
                direction,
                routeStops.Data,
                stopResults.Select(item => item.Data).ToList());
            return new ProviderQueryResult<IReadOnlyList<TransitStopResponse>>(
                mapped,
                routeStops.DataStatus == "cached" || stopResults.Any(item => item.DataStatus == "cached")
                    ? "cached"
                    : "scheduled",
                Latest([routeStops.SourceUpdatedAt, .. stopResults.Select(item => item.SourceUpdatedAt)]),
                Latest([routeStops.FetchedAt, .. stopResults.Select(item => item.FetchedAt)]),
                routeStops.Stale || stopResults.Any(item => item.Stale),
                routeStops.Message ?? stopResults.Select(item => item.Message).FirstOrDefault(item => item is not null) ??
                    (mapped.Count == 0 ? "目前查無此方向的城巴站序。" : null),
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
            var stop = await GetStopAsync(stopId, cancellationToken);
            var stopName = stop.Data.NameZh;
            var mapped = CitybusTransitMapper.MapArrivals(route, direction, stopId, stopName, eta.Data);
            if (mapped.Count > 0)
            {
                return Copy(eta, mapped, null, "realtime");
            }

            if (!eta.Stale)
            {
                var schedule = await scheduleProvider.GetScheduleAsync(cancellationToken);
                var lastDeparture = scheduleProvider.FindLastOriginDeparture(
                    "CTB",
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

            return Copy(eta, mapped, "目前查無接下來的城巴到站預報。", "realtime");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("arrival", exception);
            return Unavailable<TransitArrivalResponse>();
        }
    }

    private Task<ProviderQueryResult<IReadOnlyList<CitybusRouteRow>>> GetRoutesAsync(CancellationToken token) =>
        Cache("transit:citybus:routes:v1", NetworkFreshFor, NetworkRetainFor, apiClient.GetRoutesAsync, token);
    private Task<ProviderQueryResult<IReadOnlyList<CitybusRouteStopRow>>> GetRouteStopsAsync(
        string route,
        int direction,
        CancellationToken token) =>
        Cache($"transit:citybus:route-stops:{route}:{direction}:v1", NetworkFreshFor, NetworkRetainFor,
            ct => apiClient.GetRouteStopsAsync(route, direction, ct), token);
    private Task<ProviderQueryResult<CitybusStopRow>> GetStopAsync(string stopId, CancellationToken token) =>
        Cache($"transit:citybus:stop:{stopId}:v1", NetworkFreshFor, NetworkRetainFor,
            ct => apiClient.GetStopAsync(stopId, ct), token);
    private Task<ProviderQueryResult<IReadOnlyList<CitybusEtaRow>>> GetEtaAsync(
        string stopId,
        string route,
        CancellationToken token) =>
        Cache($"transit:citybus:eta:{stopId}:{route}:v1", RealtimeFreshFor, RealtimeRetainFor,
            ct => apiClient.GetEtaAsync(stopId, route, ct), token, "realtime");

    private async Task<ProviderQueryResult<T>> Cache<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<CitybusHttpResult<T>>> factory,
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
            "目前無法連線至香港城巴開放數據，請稍後再試。",
            timeProvider,
            Source);

    private void LogFailure(string operation, Exception exception) =>
        logger.LogWarning(exception, "Citybus {Operation} query failed ({ErrorType}).", operation, exception.GetType().Name);

    private static DateTimeOffset Latest(DateTimeOffset first, DateTimeOffset second) =>
        first >= second ? first : second;
    private static DateTimeOffset? Latest(DateTimeOffset? first, DateTimeOffset? second) =>
        first.HasValue && second.HasValue ? Latest(first.Value, second.Value) : first ?? second;
    private static DateTimeOffset Latest(IEnumerable<DateTimeOffset> values) => values.Max();
    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values) =>
        values.Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty().Max() is var value &&
        value != default
            ? value
            : null;
}
