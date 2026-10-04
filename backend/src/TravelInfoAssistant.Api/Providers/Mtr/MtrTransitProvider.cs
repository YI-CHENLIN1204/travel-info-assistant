using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Mtr;

public interface IMtrTransitProvider
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

public sealed class MtrTransitProvider(
    IMtrApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<MtrTransitProvider> logger) : IMtrTransitProvider
{
    private static readonly TimeSpan NetworkFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan NetworkRetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan RealtimeFreshFor = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RealtimeRetainFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ServiceHoursFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan ServiceHoursRetainFor = TimeSpan.FromDays(7);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            return Copy(
                network,
                MtrTransitMapper.MapRoutes(network.Data),
                "scheduled",
                "目前查無可顯示的港鐵路線。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "MTR route query failed.");
            return Unavailable<TransitRouteResponse>();
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
                MtrTransitMapper.MapStations(network.Data),
                "scheduled",
                "目前查無可顯示的港鐵車站。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "MTR station query failed.");
            return Unavailable<MetroStationResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        if (!MtrTransitMapper.TryParseStationId(stationId, out var lineCode, out var stationCode))
        {
            return Unavailable<TransitArrivalResponse>("這個車站不在目前支援的港鐵範圍內。");
        }

        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            var schedule = await GetScheduleAsync(lineCode, stationCode, cancellationToken);
            var arrivals = MtrTransitMapper.MapArrivals(
                lineCode,
                stationCode,
                schedule.Data,
                network.Data);
            var lastDepartureAt = arrivals.Count == 0
                ? await GetEndedServiceDayLastDepartureAsync(
                    lineCode,
                    stationCode,
                    network.Data,
                    cancellationToken)
                : null;
            var result = new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                arrivals,
                schedule.DataStatus == "cached" ? "cached" : "realtime",
                schedule.SourceUpdatedAt,
                schedule.FetchedAt,
                schedule.Stale,
                schedule.Message ?? (arrivals.Count == 0 ? "目前查無接下來的港鐵列車。" : null),
                "香港港鐵開放數據");
            return result with
            {
                Message = lastDepartureAt.HasValue ? null : result.Message,
                ServiceDayStatus = lastDepartureAt.HasValue ? "ended" : null,
                LastDepartureAt = lastDepartureAt
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "MTR arrival query failed for {StationId}.", stationId);
            return Unavailable<TransitArrivalResponse>();
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        string routeId,
        CancellationToken cancellationToken)
    {
        if (!MtrTransitMapper.TryParseRouteId(routeId, out var lineCode))
        {
            return Unavailable<MetroServiceStatusResponse>("請先選擇一條港鐵路線。");
        }

        try
        {
            var network = await GetNetworkAsync(cancellationToken);
            var stationCode = network.Data
                .Where(row => row.LineCode.Equals(lineCode, StringComparison.OrdinalIgnoreCase))
                .OrderBy(row => row.Sequence)
                .Select(row => row.StationCode)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(stationCode))
            {
                return Unavailable<MetroServiceStatusResponse>("目前查無這條港鐵路線的車站資料。");
            }

            var schedule = await GetScheduleAsync(lineCode, stationCode, cancellationToken);
            var status = MtrTransitMapper.MapStatus(lineCode, schedule.Data);
            return new ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>(
                status is null ? [] : [status],
                schedule.DataStatus == "cached" ? "cached" : "realtime",
                schedule.SourceUpdatedAt,
                schedule.FetchedAt,
                schedule.Stale,
                schedule.Message,
                "香港港鐵開放數據");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "MTR status query failed for {RouteId}.", routeId);
            return Unavailable<MetroServiceStatusResponse>();
        }
    }

    private async Task<ProviderQueryResult<IReadOnlyList<MtrStationRow>>> GetNetworkAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync<IReadOnlyList<MtrStationRow>>(
            "transit:mtr:network:v1",
            NetworkFreshFor,
            NetworkRetainFor,
            async token =>
            {
                var response = await apiClient.GetLinesAndStationsAsync(token);
                return new ProviderPayload<IReadOnlyList<MtrStationRow>>(
                    response.Data,
                    "scheduled",
                    response.SourceUpdatedAt,
                    response.FetchedAt);
            },
            cancellationToken);
        return result with { Source = "香港港鐵開放數據" };
    }

    private async Task<ProviderQueryResult<MtrScheduleResponse>> GetScheduleAsync(
        string lineCode,
        string stationCode,
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync<MtrScheduleResponse>(
            $"transit:mtr:schedule:v1:{lineCode.ToLowerInvariant()}:{stationCode.ToLowerInvariant()}",
            RealtimeFreshFor,
            RealtimeRetainFor,
            async token =>
            {
                var response = await apiClient.GetScheduleAsync(lineCode, stationCode, token);
                return new ProviderPayload<MtrScheduleResponse>(
                    response.Data,
                    "realtime",
                    response.SourceUpdatedAt,
                    response.FetchedAt,
                    response.SourceUpdatedAt?.AddSeconds(45));
            },
            cancellationToken);

        if (result.Stale)
        {
            throw new InvalidOperationException("Retained MTR realtime data is no longer current.");
        }

        return result with { Source = "香港港鐵開放數據" };
    }

    private async Task<DateTimeOffset?> GetEndedServiceDayLastDepartureAsync(
        string lineCode,
        string stationCode,
        IReadOnlyList<MtrStationRow> network,
        CancellationToken cancellationToken)
    {
        var stationId = network
            .FirstOrDefault(row =>
                row.LineCode.Equals(lineCode, StringComparison.OrdinalIgnoreCase) &&
                row.StationCode.Equals(stationCode, StringComparison.OrdinalIgnoreCase))
            ?.StationId;
        if (string.IsNullOrWhiteSpace(stationId))
        {
            return null;
        }

        try
        {
            var serviceHours = await cache.GetOrCreateAsync<IReadOnlyList<MtrLastTrainSchedule>>(
                $"transit:mtr:service-hours:v1:{stationId}",
                ServiceHoursFreshFor,
                ServiceHoursRetainFor,
                async token =>
                {
                    var response = await apiClient.GetLastTrainSchedulesAsync(stationId, token);
                    return new ProviderPayload<IReadOnlyList<MtrLastTrainSchedule>>(
                        response.Data,
                        "scheduled",
                        response.SourceUpdatedAt,
                        response.FetchedAt);
                },
                cancellationToken);
            if (serviceHours.Stale)
            {
                return null;
            }

            return MtrTransitMapper.GetEndedServiceDayLastDeparture(
                lineCode,
                serviceHours.Data,
                timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "MTR service-hours query failed for {LineCode}-{StationCode}.",
                lineCode,
                stationCode);
            return null;
        }
    }

    private static ProviderQueryResult<IReadOnlyList<TOutput>> Copy<TInput, TOutput>(
        ProviderQueryResult<IReadOnlyList<TInput>> source,
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
            "香港港鐵開放數據");

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(
        string message = "目前無法連線至香港港鐵開放數據，請稍後再試。") =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            [],
            message,
            timeProvider,
            "香港港鐵開放數據");
}
