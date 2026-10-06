using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public interface ILtaDataMallBusTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeQueryId,
        int direction,
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeQueryId,
        int direction,
        string stopId,
        CancellationToken cancellationToken);
}

public sealed class LtaDataMallBusTransitProvider(
    ILtaDataMallBusApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<LtaDataMallBusTransitProvider> logger) : ILtaDataMallBusTransitProvider
{
    private const string Source = "新加坡 LTA DataMall";
    private static readonly TimeSpan NetworkFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan NetworkRetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan ArrivalFreshFor = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ArrivalRetainFor = TimeSpan.FromMinutes(2);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            return Copy(
                network,
                LtaDataMallBusTransitMapper.MapRoutes(network.Data),
                "目前沒有可顯示的新加坡巴士路線。",
                "scheduled");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("route", exception);
            return Unavailable<TransitRouteResponse>(exception);
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeQueryId,
        int direction,
        CancellationToken cancellationToken)
    {
        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            return Copy(
                network,
                LtaDataMallBusTransitMapper.MapStops(routeQueryId, direction, network.Data),
                "目前沒有可顯示的新加坡巴士站序。",
                "scheduled");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("stop", exception);
            return Unavailable<TransitStopResponse>(exception);
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeQueryId,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        if (!LtaDataMallBusTransitMapper.TryParseRouteQueryId(
                routeQueryId,
                out _,
                out var serviceNo))
        {
            return Unavailable<TransitArrivalResponse>("無法辨識新加坡巴士路線。");
        }

        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            var arrivals = await GetArrivalsAsync(stopId, serviceNo, cancellationToken);
            var mapped = LtaDataMallBusTransitMapper.MapArrivals(
                routeQueryId,
                direction,
                stopId,
                arrivals.Data,
                network.Data,
                arrivals.FetchedAt);
            if (mapped.Count > 0)
            {
                return Copy(arrivals, mapped, null, "realtime");
            }

            var lastDeparture = arrivals.Stale || network.Stale
                ? null
                : LtaDataMallBusTransitMapper.GetEndedServiceDayLastDeparture(
                    routeQueryId,
                    direction,
                    stopId,
                    network.Data,
                    timeProvider.GetUtcNow());
            return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                [],
                arrivals.DataStatus == "cached" ? "cached" : "realtime",
                arrivals.SourceUpdatedAt,
                arrivals.FetchedAt,
                arrivals.Stale,
                lastDeparture.HasValue ? null : "目前沒有可用的新加坡巴士到站預估。",
                Source,
                lastDeparture.HasValue ? "ended" : null,
                lastDeparture,
                lastDeparture.HasValue ? "駛離站" : null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("arrival", exception);
            return Unavailable<TransitArrivalResponse>(exception);
        }
    }

    private async Task<ProviderQueryResult<LtaBusNetwork>> GetNetworkAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            "transit:lta:bus-network:v1",
            NetworkFreshFor,
            NetworkRetainFor,
            async token =>
            {
                var servicesTask = apiClient.GetBusServicesAsync(token);
                var routesTask = apiClient.GetBusRoutesAsync(token);
                var stopsTask = apiClient.GetBusStopsAsync(token);
                await Task.WhenAll(servicesTask, routesTask, stopsTask);
                var services = await servicesTask;
                var routes = await routesTask;
                var stops = await stopsTask;
                return new ProviderPayload<LtaBusNetwork>(
                    new LtaBusNetwork(services.Data, routes.Data, stops.Data),
                    "scheduled",
                    Latest([services.SourceUpdatedAt, routes.SourceUpdatedAt, stops.SourceUpdatedAt]),
                    Latest([services.FetchedAt, routes.FetchedAt, stops.FetchedAt]));
            },
            cancellationToken);
        return result with { Source = Source };
    }

    private async Task<ProviderQueryResult<LtaBusArrivalResponse>> GetArrivalsAsync(
        string stopId,
        string serviceNo,
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            $"transit:lta:bus-arrivals:{stopId}:{serviceNo}:v1",
            ArrivalFreshFor,
            ArrivalRetainFor,
            async token =>
            {
                var response = await apiClient.GetBusArrivalsAsync(stopId, serviceNo, token);
                return new ProviderPayload<LtaBusArrivalResponse>(
                    response.Data,
                    "realtime",
                    response.SourceUpdatedAt,
                    response.FetchedAt,
                    response.FetchedAt.AddSeconds(30));
            },
            cancellationToken);
        return result with { Source = Source };
    }

    private static ProviderQueryResult<IReadOnlyList<TOutput>> Copy<TSource, TOutput>(
        ProviderQueryResult<TSource> source,
        IReadOnlyList<TOutput> data,
        string? emptyMessage,
        string status) =>
        new(
            data,
            source.DataStatus == "cached" ? "cached" : status,
            source.SourceUpdatedAt,
            source.FetchedAt,
            source.Stale,
            source.Message ?? (data.Count == 0 ? emptyMessage : null),
            Source);

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(Exception exception) =>
        Unavailable<T>(exception is LtaDataMallNotConfiguredException
            ? "LTA DataMall API Account Key 尚未設定。"
            : "目前無法取得新加坡巴士資料，請稍後再試。");

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(string message) =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable([], message, timeProvider, Source);

    private void LogFailure(string operation, Exception exception) =>
        logger.LogWarning(
            exception,
            "LTA DataMall bus {Operation} query failed ({ErrorType}).",
            operation,
            exception.GetType().Name);

    private static DateTimeOffset Latest(IEnumerable<DateTimeOffset> values) => values.Max();
    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values) =>
        values.Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty().Max() is var value &&
        value != default
            ? value
            : null;
}
