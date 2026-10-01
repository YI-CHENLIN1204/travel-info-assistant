using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public interface ILtaDataMallTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        string routeId,
        CancellationToken cancellationToken);
}

public sealed class LtaDataMallTransitProvider(
    ILtaDataMallApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<LtaDataMallTransitProvider> logger) : ILtaDataMallTransitProvider
{
    private const string Source = "新加坡 LTA DataMall";
    private static readonly TimeSpan NetworkFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan NetworkRetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan TripUpdatesFreshFor = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TripUpdatesRetainFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AlertsFreshFor = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan AlertsRetainFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaximumRealtimeAge = TimeSpan.FromMinutes(2);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            return Copy(
                network,
                LtaDataMallTransitMapper.MapRoutes(network.Data),
                "scheduled",
                "目前查無可顯示的新加坡 MRT 路線。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("route", exception);
            return Unavailable<TransitRouteResponse>(exception);
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            return Copy(
                network,
                LtaDataMallTransitMapper.MapStations(network.Data),
                "scheduled",
                "目前查無可顯示的新加坡 MRT 車站。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("station", exception);
            return Unavailable<MetroStationResponse>(exception);
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        if (!LtaDataMallTransitMapper.TryParseStationId(
                stationId,
                out var routeId,
                out var stopId))
        {
            return Unavailable<TransitArrivalResponse>(
                "這個車站不在目前支援的新加坡 MRT 範圍內。");
        }

        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            ProviderQueryResult<LtaRealtimeFeed>? updates = null;
            try
            {
                updates = await GetTripUpdatesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailure("arrival realtime overlay", exception);
            }

            var feed = updates?.Data ?? new LtaRealtimeFeed(null, [], []);
            var arrivals = LtaDataMallTransitMapper.MapArrivals(
                routeId,
                stopId,
                feed,
                network.Data,
                timeProvider.GetUtcNow());
            var hasRealtime = arrivals.Any(item => item.EstimatedAt.HasValue);
            return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                arrivals,
                hasRealtime
                    ? updates?.DataStatus == "cached" ? "cached" : "realtime"
                    : network.DataStatus == "cached" ? "cached" : "scheduled",
                hasRealtime ? updates?.SourceUpdatedAt : network.SourceUpdatedAt,
                hasRealtime ? updates?.FetchedAt ?? network.FetchedAt : network.FetchedAt,
                hasRealtime ? updates?.Stale ?? false : network.Stale,
                arrivals.Count == 0
                    ? "目前查無接下來的新加坡 MRT 班次。"
                    : hasRealtime
                        ? updates?.Message
                        : "目前顯示官方表定班次；收到即時預估後會自動更新。",
                Source);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("arrival", exception);
            return Unavailable<TransitArrivalResponse>(exception);
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        string routeId,
        CancellationToken cancellationToken)
    {
        if (!LtaDataMallTransitMapper.TryParseRouteId(routeId, out var sourceRouteId))
        {
            return Unavailable<MetroServiceStatusResponse>("請先選擇一條新加坡 MRT 路線。");
        }

        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            var alerts = await GetAlertsAsync(cancellationToken);
            var status = LtaDataMallTransitMapper.MapStatus(
                sourceRouteId,
                alerts.Data,
                network.Data,
                timeProvider.GetUtcNow());
            return new ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>(
                status is null ? [] : [status],
                alerts.DataStatus == "cached" ? "cached" : "realtime",
                alerts.SourceUpdatedAt,
                alerts.FetchedAt,
                alerts.Stale,
                alerts.Message,
                Source);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("status", exception);
            return Unavailable<MetroServiceStatusResponse>(exception);
        }
    }

    private async Task<ProviderQueryResult<LtaGtfsNetwork>> GetNetworkAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            "transit:lta:network:v2",
            NetworkFreshFor,
            NetworkRetainFor,
            async token =>
            {
                var response = await apiClient.GetNetworkAsync(token);
                return new ProviderPayload<LtaGtfsNetwork>(
                    response.Data,
                    "scheduled",
                    response.SourceUpdatedAt,
                    response.FetchedAt);
            },
            cancellationToken);
        return result with { Source = Source };
    }

    private async Task<ProviderQueryResult<LtaRealtimeFeed>> GetTripUpdatesAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            "transit:lta:trip-updates:v1",
            TripUpdatesFreshFor,
            TripUpdatesRetainFor,
            async token =>
            {
                var response = await apiClient.GetTripUpdatesAsync(token);
                return new ProviderPayload<LtaRealtimeFeed>(
                    response.Data,
                    "realtime",
                    response.SourceUpdatedAt,
                    response.FetchedAt,
                    response.SourceUpdatedAt?.AddSeconds(90));
            },
            cancellationToken);
        return EnsureCurrent(result) with { Source = Source };
    }

    private async Task<ProviderQueryResult<LtaRealtimeFeed>> GetAlertsAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            "transit:lta:alerts:v1",
            AlertsFreshFor,
            AlertsRetainFor,
            async token =>
            {
                var response = await apiClient.GetServiceAlertsAsync(token);
                return new ProviderPayload<LtaRealtimeFeed>(
                    response.Data,
                    "realtime",
                    response.SourceUpdatedAt,
                    response.FetchedAt,
                    response.SourceUpdatedAt?.AddSeconds(90));
            },
            cancellationToken);
        return EnsureCurrent(result) with { Source = Source };
    }

    private ProviderQueryResult<LtaRealtimeFeed> EnsureCurrent(
        ProviderQueryResult<LtaRealtimeFeed> result)
    {
        if (result.Stale ||
            !result.SourceUpdatedAt.HasValue ||
            result.SourceUpdatedAt.Value < timeProvider.GetUtcNow() - MaximumRealtimeAge)
        {
            throw new LtaDataMallProviderException(
                "LTA realtime data is no longer current.");
        }
        return result;
    }

    private static ProviderQueryResult<IReadOnlyList<TOutput>> Copy<TOutput>(
        ProviderQueryResult<LtaGtfsNetwork> source,
        IReadOnlyList<TOutput> data,
        string status,
        string emptyMessage) =>
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
            : "目前無法連線至新加坡 LTA DataMall，請稍後再試。");

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(string message) =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            [],
            message,
            timeProvider,
            Source);

    private void LogFailure(string operation, Exception exception) =>
        logger.LogWarning(
            "LTA DataMall {Operation} query failed ({ErrorType}).",
            operation,
            exception.GetType().Name);
}
