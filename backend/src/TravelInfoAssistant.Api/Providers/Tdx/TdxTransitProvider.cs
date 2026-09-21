using System.Security.Cryptography;
using System.Text;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public sealed class TdxTransitProvider(
    ITdxApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<TdxTransitProvider> logger) : ITdxTransitProvider
{
    private static readonly TimeSpan StaticFreshFor = TimeSpan.FromDays(1);
    private static readonly TimeSpan StaticRetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan MetroStationFreshFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan MetroStationRetainFor = TimeSpan.FromDays(30);
    private static readonly TimeSpan RealtimeFreshFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RealtimeRetainFor = TimeSpan.FromMinutes(15);

    public Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:bus:routes:taipei:v1",
            StaticFreshFor,
            StaticRetainFor,
            Array.Empty<TransitRouteResponse>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxBusRoute>>(
                    "v2/Bus/Route/City/Taipei",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "RouteUID,RouteID,RouteName,DepartureStopNameZh," +
                                      "DepartureStopNameEn,DestinationStopNameZh," +
                                      "DestinationStopNameEn,Operators,SubRoutes,UpdateTime",
                        ["$top"] = "3000",
                        ["$format"] = "JSON"
                    },
                    token);

                var routes = response.Data
                    .Select(MapBusRoute)
                    .Where(item => item is not null)
                    .Cast<TransitRouteResponse>()
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(item => item.NameZh, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ProviderPayload<IReadOnlyList<TransitRouteResponse>>(
                    routes,
                    "scheduled",
                    Latest(response.Data.Select(item => item.UpdateTime)) ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeName,
        int direction,
        CancellationToken cancellationToken)
    {
        var feed = await GetBusStopFeedAsync(routeName, cancellationToken);
        var stops = feed.Data
            .SelectMany(item => item.Stops.Select(stop => new { item.Direction, Stop = stop }))
            .Where(item => item.Direction == direction)
            .Select(item => MapBusStop(item.Stop, item.Direction))
            .Where(item => item is not null)
            .Cast<TransitStopResponse>()
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.Sequence).First())
            .OrderBy(item => item.Sequence)
            .ToList();

        return CopyMetadata<IReadOnlyList<TdxBusStopOfRoute>, IReadOnlyList<TransitStopResponse>>(
            feed,
            stops);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeName,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        var feed = await GetBusArrivalFeedAsync(routeName, cancellationToken);
        var matching = feed.Data
            .Where(item => item.Direction == direction)
            .Where(item => string.Equals(
                item.StopUID ?? item.StopID,
                stopId,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        var arrivals = matching
            .SelectMany(item => MapBusArrivals(item, feed.FetchedAt))
            .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
            .ToList();

        return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
            arrivals,
            feed.DataStatus,
            Latest(matching.Select(GetBusSourceUpdatedAt)) ?? feed.SourceUpdatedAt,
            feed.FetchedAt,
            feed.Stale,
            feed.Message);
    }

    public Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:metro:stations:trtc:v1",
            MetroStationFreshFor,
            MetroStationRetainFor,
            Array.Empty<MetroStationResponse>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxMetroStation>>(
                    "v2/Rail/Metro/Station/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "StationPosition,StationUID,StationID,StationName," +
                                      "StationAddress,SrcUpdateTime,UpdateTime",
                        ["$top"] = "500",
                        ["$format"] = "JSON"
                    },
                    token);

                var stations = response.Data
                    .Select(MapMetroStation)
                    .Where(item => item is not null)
                    .Cast<MetroStationResponse>()
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(item => item.NameZh, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ProviderPayload<IReadOnlyList<MetroStationResponse>>(
                    stations,
                    "scheduled",
                    Latest(response.Data.Select(item => item.SrcUpdateTime ?? item.UpdateTime))
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var timetable = await GetMetroTimetableAsync(stationId, cancellationToken);
        var liveBoard = await GetMetroLiveBoardAsync(stationId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var scheduled = BuildScheduledMetroArrivals(stationId, timetable.Data, now);

        if (liveBoard.Data.Count == 0)
        {
            if (scheduled.Count > 0)
            {
                return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                    scheduled,
                    timetable.DataStatus == "unavailable" ? "scheduled" : timetable.DataStatus,
                    timetable.SourceUpdatedAt,
                    timetable.FetchedAt,
                    timetable.Stale,
                    liveBoard.DataStatus == "unavailable"
                        ? "即時列車資料暫時無法更新，目前顯示表定時刻。"
                        : "目前沒有即時列車資料，顯示表定時刻。");
            }

            return ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>.Unavailable(
                Array.Empty<TransitArrivalResponse>(),
                liveBoard.Message ?? timetable.Message ?? "目前查無可顯示的捷運班次。",
                timeProvider);
        }

        var arrivals = liveBoard.Data
            .Select((item, index) => MapMetroLiveArrival(item, scheduled, now, index))
            .Where(item => item is not null)
            .Cast<TransitArrivalResponse>()
            .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
            .ToList();

        return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
            arrivals,
            liveBoard.DataStatus,
            liveBoard.SourceUpdatedAt,
            liveBoard.FetchedAt,
            liveBoard.Stale,
            liveBoard.Message ?? timetable.Message);
    }

    private Task<ProviderQueryResult<IReadOnlyList<TdxMetroLiveBoard>>> GetMetroLiveBoardAsync(
        string stationId,
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            $"transit:tdx:metro:live:trtc:{KeyPart(stationId)}:v1",
            RealtimeFreshFor,
            RealtimeRetainFor,
            Array.Empty<TdxMetroLiveBoard>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxMetroLiveBoard>>(
                    "v2/Rail/Metro/LiveBoard/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "LineNO,LineID,LineName,StationID,StationName,TripHeadSign," +
                                      "DestinationStaionID,DestinationStationID," +
                                      "DestinationStationName,ServiceStatus,EstimateTime," +
                                      "SrcUpdateTime,UpdateTime",
                        ["$filter"] = $"StationID eq '{EscapeOData(stationId)}'",
                        ["$top"] = "100",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxMetroLiveBoard>>(
                    response.Data,
                    "realtime",
                    Latest(response.Data.Select(item => item.SrcUpdateTime ?? item.UpdateTime))
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<IReadOnlyList<TdxBusStopOfRoute>>> GetBusStopFeedAsync(
        string routeName,
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            $"transit:tdx:bus:stops:taipei:{KeyPart(routeName)}:v1",
            StaticFreshFor,
            StaticRetainFor,
            Array.Empty<TdxBusStopOfRoute>(),
            async token =>
            {
                var path = $"v2/Bus/StopOfRoute/City/Taipei/{Uri.EscapeDataString(routeName)}";
                var response = await apiClient.GetAsync<IReadOnlyList<TdxBusStopOfRoute>>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "RouteUID,RouteID,RouteName,SubRouteUID,Direction,Stops,UpdateTime",
                        ["$top"] = "100",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxBusStopOfRoute>>(
                    response.Data,
                    "scheduled",
                    Latest(response.Data.Select(item => item.UpdateTime)) ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<IReadOnlyList<TdxBusArrival>>> GetBusArrivalFeedAsync(
        string routeName,
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            $"transit:tdx:bus:arrivals:taipei:{KeyPart(routeName)}:v1",
            RealtimeFreshFor,
            RealtimeRetainFor,
            Array.Empty<TdxBusArrival>(),
            async token =>
            {
                var path =
                    $"v2/Bus/EstimatedTimeOfArrival/City/Taipei/{Uri.EscapeDataString(routeName)}";
                var response = await apiClient.GetAsync<IReadOnlyList<TdxBusArrival>>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "PlateNumb,StopUID,StopID,StopName,RouteUID,RouteID," +
                                      "RouteName,Direction,EstimateTime,ScheduledTime,StopStatus," +
                                      "NextBusTime,IsLastBus,Estimates,DataTime,SrcUpdateTime,UpdateTime",
                        ["$top"] = "2000",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxBusArrival>>(
                    response.Data,
                    "realtime",
                    Latest(response.Data.Select(GetBusSourceUpdatedAt)) ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<IReadOnlyList<TdxMetroStationTimetable>>> GetMetroTimetableAsync(
        string stationId,
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            $"transit:tdx:metro:timetable:trtc:{KeyPart(stationId)}:v1",
            StaticFreshFor,
            StaticRetainFor,
            Array.Empty<TdxMetroStationTimetable>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxMetroStationTimetable>>(
                    "v2/Rail/Metro/StationTimeTable/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "RouteID,LineID,StationID,StationName,Direction," +
                                      "DestinationStaionID,DestinationStationName,Timetables," +
                                      "ServiceDay,SrcUpdateTime,UpdateTime",
                        ["$filter"] = $"StationID eq '{EscapeOData(stationId)}'",
                        ["$top"] = "100",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxMetroStationTimetable>>(
                    response.Data,
                    "scheduled",
                    Latest(response.Data.Select(item => item.SrcUpdateTime ?? item.UpdateTime))
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private async Task<ProviderQueryResult<T>> GetCachedSafelyAsync<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        T fallback,
        Func<CancellationToken, Task<ProviderPayload<T>>> factory,
        CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                freshFor,
                retainFor,
                factory,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "TDX query failed for {CacheKey}.", key);
            return ProviderQueryResult<T>.Unavailable(
                fallback,
                GetPublicErrorMessage(exception),
                timeProvider);
        }
    }

    private static ProviderQueryResult<TTarget> CopyMetadata<TSource, TTarget>(
        ProviderQueryResult<TSource> source,
        TTarget data) =>
        new(
            data,
            source.DataStatus,
            source.SourceUpdatedAt,
            source.FetchedAt,
            source.Stale,
            source.Message);

    private static TransitRouteResponse? MapBusRoute(TdxBusRoute route)
    {
        var id = route.RouteUID ?? route.RouteID;
        var nameZh = PreferredName(route.RouteName);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        var directions = route.SubRoutes
            .GroupBy(item => item.Direction)
            .Select(group => group.First())
            .Select(item => new TransitDirectionResponse(
                item.Direction,
                FirstText(item.Headsign, PreferredName(item.SubRouteName)),
                FirstText(item.DepartureStopNameZh, route.DepartureStopNameZh),
                FirstText(item.DestinationStopNameZh, route.DestinationStopNameZh)))
            .OrderBy(item => item.Direction)
            .ToList();

        if (directions.Count == 0)
        {
            directions.Add(new TransitDirectionResponse(
                0,
                route.DestinationStopNameZh,
                route.DepartureStopNameZh,
                route.DestinationStopNameZh));
        }

        var operators = route.Operators
            .Select(item => PreferredName(item.OperatorName))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TransitRouteResponse(
            id,
            nameZh,
            route.RouteName?.En,
            route.DepartureStopNameZh,
            route.DestinationStopNameZh,
            operators,
            directions);
    }

    private static TransitStopResponse? MapBusStop(TdxBusStop stop, int direction)
    {
        var id = stop.StopUID ?? stop.StopID;
        var nameZh = PreferredName(stop.StopName);
        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh)
            ? null
            : new TransitStopResponse(
                id,
                nameZh,
                stop.StopName?.En,
                stop.StopSequence,
                direction,
                stop.StopPosition?.PositionLat,
                stop.StopPosition?.PositionLon);
    }

    private static IReadOnlyList<TransitArrivalResponse> MapBusArrivals(
        TdxBusArrival item,
        DateTimeOffset fetchedAt)
    {
        var stopId = item.StopUID ?? item.StopID;
        var stopName = PreferredName(item.StopName);
        if (string.IsNullOrWhiteSpace(stopId) || string.IsNullOrWhiteSpace(stopName))
        {
            return [];
        }

        IReadOnlyList<ArrivalEstimate> estimates = item.Estimates.Count > 0
            ? item.Estimates
                .Select(value => new ArrivalEstimate(
                    value.PlateNumb,
                    value.EstimateTime,
                    value.IsLastBus))
                .ToList()
            : [new ArrivalEstimate(item.PlateNumb, item.EstimateTime, item.IsLastBus)];

        var sourceUpdatedAt = GetBusSourceUpdatedAt(item);
        var estimateBase = sourceUpdatedAt ?? fetchedAt;
        var scheduledAt = item.NextBusTime
            ?? TdxTimeParser.ParseNextOccurrence(item.ScheduledTime, fetchedAt);

        return estimates
            .Select((estimate, index) =>
            {
                var estimatedAt = estimate.EstimateTime is >= 0
                    ? estimateBase.AddSeconds(estimate.EstimateTime.Value)
                    : (DateTimeOffset?)null;
                return new TransitArrivalResponse(
                    $"bus:{item.RouteUID ?? item.RouteID}:{item.Direction}:{stopId}:{estimate.PlateNumb ?? index.ToString()}",
                    "bus",
                    stopId,
                    stopName,
                    item.RouteUID ?? item.RouteID,
                    PreferredName(item.RouteName),
                    null,
                    null,
                    null,
                    item.Direction,
                    scheduledAt ?? estimatedAt,
                    estimatedAt,
                    sourceUpdatedAt,
                    GetBusStopStatus(item.StopStatus),
                    estimate.IsLastBus);
            })
            .ToList();
    }

    private static MetroStationResponse? MapMetroStation(TdxMetroStation station)
    {
        var id = station.StationID ?? station.StationUID;
        var nameZh = PreferredName(station.StationName);
        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh)
            ? null
            : new MetroStationResponse(
                id,
                nameZh,
                station.StationName?.En,
                station.StationAddress,
                station.StationPosition?.PositionLat,
                station.StationPosition?.PositionLon);
    }

    private static IReadOnlyList<TransitArrivalResponse> BuildScheduledMetroArrivals(
        string stationId,
        IReadOnlyList<TdxMetroStationTimetable> timetables,
        DateTimeOffset now)
    {
        var arrivals = new List<TransitArrivalResponse>();
        foreach (var timetable in timetables.Where(item =>
                     TdxTimeParser.IsServiceDay(item.ServiceDay, now)))
        {
            foreach (var entry in timetable.Timetables)
            {
                var scheduledAt = TdxTimeParser.ParseNextOccurrence(
                    entry.ArrivalTime ?? entry.DepartureTime,
                    now);
                if (scheduledAt is null || scheduledAt < now.AddMinutes(-2) ||
                    scheduledAt > now.AddHours(3))
                {
                    continue;
                }

                arrivals.Add(new TransitArrivalResponse(
                    $"metro-schedule:{stationId}:{timetable.LineID}:{timetable.Direction}:{entry.Sequence}",
                    "metro",
                    stationId,
                    PreferredName(timetable.StationName),
                    timetable.RouteID,
                    null,
                    timetable.LineID,
                    timetable.LineID,
                    PreferredName(timetable.DestinationStationName),
                    timetable.Direction,
                    scheduledAt,
                    null,
                    timetable.SrcUpdateTime ?? timetable.UpdateTime,
                    "表定班次",
                    false));
            }
        }

        return arrivals
            .OrderBy(item => item.ScheduledAt)
            .Take(12)
            .ToList();
    }

    private static TransitArrivalResponse? MapMetroLiveArrival(
        TdxMetroLiveBoard item,
        IReadOnlyList<TransitArrivalResponse> scheduled,
        DateTimeOffset now,
        int index)
    {
        var stationName = PreferredName(item.StationName);
        if (string.IsNullOrWhiteSpace(item.StationID) || string.IsNullOrWhiteSpace(stationName))
        {
            return null;
        }

        var sourceUpdatedAt = item.SrcUpdateTime ?? item.UpdateTime;
        var estimateBase = sourceUpdatedAt ?? now;
        var estimatedAt = item.EstimateTime is >= 0
            // Unlike the bus endpoint (seconds), Metro LiveBoard reports minutes.
            ? estimateBase.AddMinutes(item.EstimateTime.Value)
            : (DateTimeOffset?)null;
        var destination = PreferredName(item.DestinationStationName);
        var matchingSchedule = scheduled
            .Where(value => string.Equals(value.LineId, item.LineID, StringComparison.OrdinalIgnoreCase))
            .Where(value => string.IsNullOrWhiteSpace(destination) ||
                            string.Equals(
                                value.DestinationName,
                                destination,
                                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.ScheduledAt)
            .FirstOrDefault(value => value.ScheduledAt >= now.AddMinutes(-2));

        return new TransitArrivalResponse(
            $"metro-live:{item.StationID}:{item.LineID}:{item.DestinationStationID ?? item.DestinationStaionID}:{index}",
            "metro",
            item.StationID,
            stationName,
            null,
            null,
            item.LineID,
            FirstText(PreferredName(item.LineName), item.LineID),
            FirstText(destination, item.TripHeadSign),
            matchingSchedule?.Direction,
            matchingSchedule?.ScheduledAt ?? estimatedAt,
            estimatedAt,
            sourceUpdatedAt,
            GetMetroServiceStatus(item.ServiceStatus),
            false);
    }

    private static string GetPublicErrorMessage(Exception exception) => exception switch
    {
        TdxNotConfiguredException =>
            "TDX 金鑰尚未設定；功能入口會保留，但目前無法取得真實資料。",
        TdxQuotaExceededException =>
            "本月 TDX 免費額度已達內部停止線，暫停新的外部查詢以避免產生費用。",
        TdxRateLimitException =>
            "目前查詢較頻繁，請稍後再試；系統不會因此增加付費用量。",
        TdxProviderException => "TDX 暫時無法提供資料，請稍後再試。",
        HttpRequestException => "目前無法連線至 TDX，請稍後再試。",
        _ => "交通資料暫時無法更新，請稍後再試。"
    };

    private static DateTimeOffset? GetBusSourceUpdatedAt(TdxBusArrival item) =>
        item.DataTime ?? item.SrcUpdateTime ?? item.UpdateTime;

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values)
    {
        var latest = values
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();
        return latest == default ? null : latest;
    }

    private static string PreferredName(TdxLocalizedName? value) =>
        FirstText(value?.ZhTw, value?.En) ?? string.Empty;

    private static string? FirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string GetBusStopStatus(int status) => status switch
    {
        0 => "正常營運",
        1 => "尚未發車",
        2 => "交管不停靠",
        3 => "末班車已過",
        4 => "今日未營運",
        _ => "狀態待確認"
    };

    private static string GetMetroServiceStatus(int status) => status switch
    {
        0 => "正常營運",
        1 => "尚未發車",
        2 => "交管不停靠",
        3 => "末班車已過",
        4 => "今日未營運",
        _ => "狀態待確認"
    };

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string KeyPart(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..20];

    private sealed record ArrivalEstimate(
        string? PlateNumb,
        int? EstimateTime,
        bool IsLastBus);
}
