using System.Globalization;
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
    private static readonly TimeSpan RailTimetableFreshFor = TimeSpan.FromHours(4);
    private static readonly TimeSpan RailTimetableRetainFor = TimeSpan.FromDays(1);
    private static readonly TimeSpan RealtimeFreshFor = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RealtimeRetainFor = TimeSpan.FromMinutes(15);

    public Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            // v2 fixes direction metadata. Keep the version in the key so an older
            // mapped route response cannot survive in Redis after deployment.
            "transit:tdx:bus:routes:taipei:v2",
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
            // TDX can return the shared portion of multiple sub-routes with different StopUIDs.
            // Treat an equal direction, sequence and normalized name as one user-facing stop.
            .GroupBy(
                item => LogicalStopKey(item.Sequence, item.NameZh),
                StringComparer.OrdinalIgnoreCase)
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
        var stopFeed = await GetBusStopFeedAsync(routeName, cancellationToken);
        var equivalentStopIds = GetEquivalentBusStopIds(
            stopFeed.Data,
            direction,
            stopId);
        var feed = await GetBusArrivalFeedAsync(routeName, cancellationToken);
        var matching = feed.Data
            .Where(item => item.Direction == direction)
            .Where(item =>
            {
                var providerStopId = FirstText(item.StopUID, item.StopID);
                return providerStopId is not null && equivalentStopIds.Contains(providerStopId);
            })
            .ToList();
        var arrivals = matching
            .SelectMany(item => MapBusArrivals(item, feed.FetchedAt, stopId))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
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

    public async Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken)
    {
        var lineTask = GetMetroLineFeedAsync(cancellationToken);
        var routeTask = GetMetroStationOfRouteFeedAsync(cancellationToken);
        await Task.WhenAll(lineTask, routeTask);
        var lines = await lineTask;
        var routes = await routeTask;
        var lineNames = lines.Data
            .Where(item => !string.IsNullOrWhiteSpace(item.LineID))
            .GroupBy(item => item.LineID!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => PreferredName(group.First().LineName),
                StringComparer.OrdinalIgnoreCase);
        var stations = routes.Data
            .Where(item => !string.IsNullOrWhiteSpace(item.LineID))
            .SelectMany(route => route.Stations.Select(station =>
                MapMetroRouteStation(route, station, lineNames)))
            .Where(item => item is not null)
            .Cast<MetroStationResponse>()
            .GroupBy(
                item => $"{item.RailwayId}:{item.Id}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.RailwayId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return CombineMetroNetworkMetadata<IReadOnlyList<MetroStationResponse>>(
            lines,
            routes,
            stations);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken)
    {
        var lineTask = GetMetroLineFeedAsync(cancellationToken);
        var routeTask = GetMetroStationOfRouteFeedAsync(cancellationToken);
        await Task.WhenAll(lineTask, routeTask);
        var lines = await lineTask;
        var routes = await routeTask;
        var mapped = routes.Data
            .Where(item => !string.IsNullOrWhiteSpace(item.LineID))
            .GroupBy(item => item.LineID!, StringComparer.OrdinalIgnoreCase)
            .Select(group => MapMetroRoute(
                group.Key,
                lines.Data.FirstOrDefault(line => string.Equals(
                    line.LineID,
                    group.Key,
                    StringComparison.OrdinalIgnoreCase)),
                group))
            .Where(item => item is not null)
            .Cast<TransitRouteResponse>()
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return CombineMetroNetworkMetadata<IReadOnlyList<TransitRouteResponse>>(
            lines,
            routes,
            mapped);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        string routeId,
        CancellationToken cancellationToken)
    {
        var lineId = ParseMetroLineId(routeId);
        if (lineId is null)
        {
            return ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>.Unavailable(
                Array.Empty<MetroServiceStatusResponse>(),
                "無法辨識這條台北捷運路線。",
                timeProvider);
        }

        var feed = await GetMetroAlertFeedAsync(cancellationToken);
        var intervalSeconds = Math.Max(
            60,
            feed.Data.SrcUpdateInterval ?? feed.Data.UpdateInterval ?? 60);
        var statuses = feed.Data.Alerts
            .Where(alert => AppliesToMetroLine(alert.Scope, lineId))
            .Select((alert, index) =>
            {
                var updatedAt = alert.UpdateTime
                                ?? feed.Data.SrcUpdateTime
                                ?? feed.Data.UpdateTime
                                ?? feed.SourceUpdatedAt;
                return new MetroServiceStatusResponse(
                    $"tdx-metro-alert:{alert.AlertID ?? index.ToString(CultureInfo.InvariantCulture)}:{lineId}",
                    routeId,
                    lineId,
                    null,
                    null,
                    updatedAt,
                    updatedAt?.AddSeconds(intervalSeconds),
                    FirstText(alert.Description, alert.Title, "目前沒有官方服務警示。"));
            })
            .ToList();

        return CopyMetadata<TdxMetroAlertResponse, IReadOnlyList<MetroServiceStatusResponse>>(
            feed,
            statuses);
    }

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

            var lastDepartureAt = GetEndedMetroServiceDayLastDeparture(timetable.Data, now);
            if (lastDepartureAt.HasValue)
            {
                return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                    [],
                    timetable.DataStatus == "unavailable" ? "scheduled" : timetable.DataStatus,
                    timetable.SourceUpdatedAt,
                    timetable.FetchedAt,
                    timetable.Stale,
                    null,
                    ServiceDayStatus: "ended",
                    LastDepartureAt: lastDepartureAt);
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
        var endedAt = arrivals.Count == 0
            ? GetEndedMetroServiceDayLastDeparture(timetable.Data, now)
            : null;

        return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
            arrivals,
            liveBoard.DataStatus,
            liveBoard.SourceUpdatedAt,
            liveBoard.FetchedAt,
            liveBoard.Stale,
            liveBoard.Message ?? timetable.Message,
            ServiceDayStatus: endedAt.HasValue ? "ended" : null,
            LastDepartureAt: endedAt);
    }

    public Task<ProviderQueryResult<IReadOnlyList<RailStationResponse>>> GetRailStationsAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:rail:stations:tra:v2",
            MetroStationFreshFor,
            MetroStationRetainFor,
            Array.Empty<RailStationResponse>(),
            async token =>
            {
                var stationTask = apiClient.GetAsync<TdxTraStationResponse>(
                    "v3/Rail/TRA/Station",
                    new Dictionary<string, string?>
                    {
                        ["$top"] = "1000",
                        ["$format"] = "JSON"
                    },
                    token);
                var stationOfLineTask = apiClient.GetAsync<TdxTraStationOfLineResponse>(
                    "v3/Rail/TRA/StationOfLine",
                    new Dictionary<string, string?>
                    {
                        ["$top"] = "100",
                        ["$format"] = "JSON"
                    },
                    token);
                await Task.WhenAll(stationTask, stationOfLineTask);
                var response = await stationTask;
                var stationOfLineResponse = await stationOfLineTask;
                var positionsByStation = BuildRailStationPositions(
                    stationOfLineResponse.Data.StationOfLines);

                var stations = response.Data.Stations
                    .Select(station => MapRailStation(station, positionsByStation))
                    .Where(item => item is not null)
                    .Cast<RailStationResponse>()
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(item => item.LinePositions.FirstOrDefault()?.LineId,
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.LinePositions.FirstOrDefault()?.Sequence ?? int.MaxValue)
                    .ThenBy(item => item.NameZh, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ProviderPayload<IReadOnlyList<RailStationResponse>>(
                    stations,
                    "scheduled",
                    Latest([
                        response.Data.SrcUpdateTime ?? response.Data.UpdateTime ?? response.LastModified,
                        stationOfLineResponse.Data.SrcUpdateTime
                            ?? stationOfLineResponse.Data.UpdateTime
                            ?? stationOfLineResponse.LastModified
                    ]),
                    response.FetchedAt > stationOfLineResponse.FetchedAt
                        ? response.FetchedAt
                        : stationOfLineResponse.FetchedAt);
            },
            cancellationToken);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetRailArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var stationsTask = GetRailStationsAsync(cancellationToken);
        var timetableTask = GetRailTimetableAsync(stationId, cancellationToken);
        var liveBoardTask = GetRailLiveBoardAsync(stationId, cancellationToken);
        await Task.WhenAll(stationsTask, timetableTask, liveBoardTask);
        var stations = await stationsTask;
        var timetable = await timetableTask;
        var liveBoard = await liveBoardTask;
        var stationsById = stations.Data
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        var now = timeProvider.GetUtcNow();
        var scheduled = BuildScheduledRailArrivals(
            stationId,
            timetable.Data.StationTimetables,
            timetable.SourceUpdatedAt,
            now,
            stationsById);

        if (liveBoard.Data.StationLiveBoards.Count == 0)
        {
            var nextScheduled = SelectNextRailArrivals(scheduled, [], now);
            var lastDepartureAt = nextScheduled.Count == 0
                ? GetEndedServiceDayLastDeparture(timetable.Data.StationTimetables, now)
                : null;
            if (lastDepartureAt.HasValue)
            {
                return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                    [],
                    timetable.DataStatus == "unavailable" ? "scheduled" : timetable.DataStatus,
                    timetable.SourceUpdatedAt,
                    timetable.FetchedAt,
                    timetable.Stale,
                    null,
                    ServiceDayStatus: "ended",
                    LastDepartureAt: lastDepartureAt);
            }

            if (nextScheduled.Count > 0)
            {
                return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                    nextScheduled,
                    timetable.DataStatus == "unavailable" ? "scheduled" : timetable.DataStatus,
                    timetable.SourceUpdatedAt,
                    timetable.FetchedAt,
                    timetable.Stale,
                    liveBoard.DataStatus == "unavailable"
                        ? "台鐵即時資料暫時無法更新，目前顯示表定時刻。"
                        : "目前沒有台鐵即時資料，顯示表定時刻。");
            }

            return ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>.Unavailable(
                Array.Empty<TransitArrivalResponse>(),
                liveBoard.Message ?? timetable.Message ?? "目前查無可顯示的台鐵班次。",
                timeProvider);
        }

        var liveArrivals = liveBoard.Data.StationLiveBoards
            .Select((item, index) => MapRailLiveArrival(
                item,
                stationId,
                scheduled,
                stationsById,
                liveBoard.SourceUpdatedAt,
                now,
                index))
            .Where(item => item is not null)
            .Cast<TransitArrivalResponse>()
            .ToList();
        var arrivals = SelectNextRailArrivals(scheduled, liveArrivals, now);
        var endedAt = arrivals.Count == 0
            ? GetEndedServiceDayLastDeparture(timetable.Data.StationTimetables, now)
            : null;

        return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
            arrivals,
            liveBoard.DataStatus,
            liveBoard.SourceUpdatedAt,
            liveBoard.FetchedAt,
            liveBoard.Stale,
            liveBoard.Message ?? timetable.Message,
            ServiceDayStatus: endedAt.HasValue ? "ended" : null,
            LastDepartureAt: endedAt);
    }

    private Task<ProviderQueryResult<TdxTraDailyStationTimetableResponse>> GetRailTimetableAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var localDate = TdxTimeParser.ToTaipei(timeProvider.GetUtcNow())
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return GetCachedSafelyAsync(
            $"transit:tdx:rail:timetable:tra:{KeyPart(stationId)}:{localDate}:v1",
            RailTimetableFreshFor,
            RailTimetableRetainFor,
            new TdxTraDailyStationTimetableResponse(),
            async token =>
            {
                var path = "v3/Rail/TRA/DailyStationTimetable/Today/Station/" +
                           Uri.EscapeDataString(stationId);
                var response = await apiClient.GetAsync<TdxTraDailyStationTimetableResponse>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["$top"] = "500",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<TdxTraDailyStationTimetableResponse>(
                    response.Data,
                    "scheduled",
                    response.Data.SrcUpdateTime ?? response.Data.UpdateTime ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);
    }

    private Task<ProviderQueryResult<TdxTraStationLiveBoardResponse>> GetRailLiveBoardAsync(
        string stationId,
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            $"transit:tdx:rail:live:tra:{KeyPart(stationId)}:v1",
            RealtimeFreshFor,
            RealtimeRetainFor,
            new TdxTraStationLiveBoardResponse(),
            async token =>
            {
                var path = "v3/Rail/TRA/StationLiveBoard/Station/" +
                           Uri.EscapeDataString(stationId);
                var response = await apiClient.GetAsync<TdxTraStationLiveBoardResponse>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["$top"] = "200",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<TdxTraStationLiveBoardResponse>(
                    response.Data,
                    "realtime",
                    Latest(response.Data.StationLiveBoards.Select(item => item.UpdateTime))
                        ?? response.Data.SrcUpdateTime
                        ?? response.Data.UpdateTime
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<IReadOnlyList<TdxMetroLine>>> GetMetroLineFeedAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:metro:lines:trtc:v1",
            MetroStationFreshFor,
            MetroStationRetainFor,
            Array.Empty<TdxMetroLine>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxMetroLine>>(
                    "v2/Rail/Metro/Line/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "LineNO,LineID,LineName,SrcUpdateTime,UpdateTime",
                        ["$top"] = "100",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxMetroLine>>(
                    response.Data,
                    "scheduled",
                    Latest(response.Data.Select(item => item.SrcUpdateTime ?? item.UpdateTime))
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<IReadOnlyList<TdxMetroStationOfRoute>>>
        GetMetroStationOfRouteFeedAsync(CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:metro:station-of-route:trtc:v1",
            MetroStationFreshFor,
            MetroStationRetainFor,
            Array.Empty<TdxMetroStationOfRoute>(),
            async token =>
            {
                var response = await apiClient.GetAsync<IReadOnlyList<TdxMetroStationOfRoute>>(
                    "v2/Rail/Metro/StationOfRoute/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$select"] = "LineNO,LineID,RouteID,RouteName,Direction,Stations," +
                                      "SrcUpdateTime,UpdateTime",
                        ["$top"] = "500",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<IReadOnlyList<TdxMetroStationOfRoute>>(
                    response.Data,
                    "scheduled",
                    Latest(response.Data.Select(item => item.SrcUpdateTime ?? item.UpdateTime))
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

    private Task<ProviderQueryResult<TdxMetroAlertResponse>> GetMetroAlertFeedAsync(
        CancellationToken cancellationToken) =>
        GetCachedSafelyAsync(
            "transit:tdx:metro:alerts:trtc:v1",
            TimeSpan.FromMinutes(1),
            RealtimeRetainFor,
            new TdxMetroAlertResponse(),
            async token =>
            {
                var response = await apiClient.GetAsync<TdxMetroAlertResponse>(
                    "v2/Rail/Metro/Alert/TRTC",
                    new Dictionary<string, string?>
                    {
                        ["$top"] = "200",
                        ["$format"] = "JSON"
                    },
                    token);

                return new ProviderPayload<TdxMetroAlertResponse>(
                    response.Data,
                    "realtime",
                    Latest(response.Data.Alerts.Select(item => item.UpdateTime))
                        ?? response.Data.SrcUpdateTime
                        ?? response.Data.UpdateTime
                        ?? response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);

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

    private static ProviderQueryResult<TTarget> CombineMetroNetworkMetadata<TTarget>(
        ProviderQueryResult<IReadOnlyList<TdxMetroLine>> lines,
        ProviderQueryResult<IReadOnlyList<TdxMetroStationOfRoute>> routes,
        TTarget data) =>
        new(
            data,
            routes.DataStatus == "unavailable" ? routes.DataStatus : lines.DataStatus,
            Latest([lines.SourceUpdatedAt, routes.SourceUpdatedAt]),
            lines.FetchedAt > routes.FetchedAt ? lines.FetchedAt : routes.FetchedAt,
            lines.Stale || routes.Stale,
            routes.Message ?? lines.Message);

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
            .Select(group => MapBusDirection(route, group.Key, group))
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
            directions,
            []);
    }

    private static TransitDirectionResponse MapBusDirection(
        TdxBusRoute route,
        int direction,
        IEnumerable<TdxBusSubRoute> subRoutes)
    {
        var items = subRoutes.ToList();
        var origin = items
            .Select(item => item.DepartureStopNameZh)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        var destination = items
            .Select(item => item.DestinationStopNameZh)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));

        var fallbackOrigin = direction == 1
            ? route.DestinationStopNameZh
            : route.DepartureStopNameZh;
        var fallbackDestination = direction == 1
            ? route.DepartureStopNameZh
            : route.DestinationStopNameZh;

        // Some providers repeat the route-level outbound endpoints inside both
        // sub-route directions. For the return direction that pair must be reversed.
        if (direction == 1 &&
            SameText(origin, route.DepartureStopNameZh) &&
            SameText(destination, route.DestinationStopNameZh))
        {
            origin = null;
            destination = null;
        }

        return new TransitDirectionResponse(
            direction,
            items.Select(item => item.Headsign)
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)),
            FirstText(origin, fallbackOrigin),
            FirstText(destination, fallbackDestination));
    }

    private static bool SameText(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

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
        DateTimeOffset fetchedAt,
        string? canonicalStopId = null)
    {
        var providerStopId = FirstText(item.StopUID, item.StopID);
        var stopId = FirstText(canonicalStopId, providerStopId);
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

    private static HashSet<string> GetEquivalentBusStopIds(
        IReadOnlyList<TdxBusStopOfRoute> routes,
        int direction,
        string selectedStopId)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            selectedStopId
        };
        var stops = routes
            .Where(item => item.Direction == direction)
            .SelectMany(item => item.Stops)
            .ToList();
        var selected = stops.FirstOrDefault(stop => string.Equals(
            FirstText(stop.StopUID, stop.StopID),
            selectedStopId,
            StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return result;
        }

        var selectedKey = LogicalStopKey(
            selected.StopSequence,
            PreferredName(selected.StopName));
        foreach (var stop in stops.Where(stop => string.Equals(
                     LogicalStopKey(stop.StopSequence, PreferredName(stop.StopName)),
                     selectedKey,
                     StringComparison.OrdinalIgnoreCase)))
        {
            var id = FirstText(stop.StopUID, stop.StopID);
            if (id is not null)
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static string LogicalStopKey(int sequence, string name) =>
        $"{sequence}:{NormalizeStopName(name)}";

    private static string NormalizeStopName(string value)
    {
        var normalized = value
            .Normalize(NormalizationForm.FormKC)
            .Replace('臺', '台');
        return string.Concat(normalized.Where(character => !char.IsWhiteSpace(character)));
    }

    private static TransitRouteResponse? MapMetroRoute(
        string lineId,
        TdxMetroLine? line,
        IEnumerable<TdxMetroStationOfRoute> routeVariants)
    {
        var variants = routeVariants
            .Where(item => item.Stations.Count > 0)
            .ToList();
        if (variants.Count == 0)
        {
            return null;
        }

        var nameZh = FirstText(PreferredName(line?.LineName), lineId)!;
        var directions = variants
            .GroupBy(item => item.Direction)
            .Select(group => MapMetroDirection(group.Key, group))
            .OrderBy(item => item.Direction)
            .ToList();
        var primaryDirection = directions.FirstOrDefault(item => item.Direction == 0)
                               ?? directions.FirstOrDefault();
        var stationNames = variants
            .Where(item => item.Direction == (primaryDirection?.Direction ?? variants[0].Direction))
            .OrderByDescending(item => item.Stations.Count)
            .SelectMany(item => item.Stations.OrderBy(station => station.Sequence))
            .Select(station => PreferredName(station.StationName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TransitRouteResponse(
            $"TDX:TRTC:{lineId}",
            nameZh,
            line?.LineName?.En,
            primaryDirection?.OriginName,
            primaryDirection?.DestinationName,
            ["台北捷運"],
            directions,
            stationNames);
    }

    private static TransitDirectionResponse MapMetroDirection(
        int direction,
        IEnumerable<TdxMetroStationOfRoute> routeVariants)
    {
        var paths = routeVariants
            .Select(route => route.Stations
                .OrderBy(station => station.Sequence)
                .Where(station => !string.IsNullOrWhiteSpace(station.StationID))
                .ToList())
            .Where(path => path.Count > 0)
            .ToList();
        var names = paths
            .SelectMany(path => path)
            .GroupBy(station => station.StationID!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => PreferredName(group.First().StationName),
                StringComparer.OrdinalIgnoreCase);
        var incoming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var outgoing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            for (var index = 0; index < path.Count - 1; index++)
            {
                outgoing.Add(path[index].StationID!);
                incoming.Add(path[index + 1].StationID!);
            }
        }

        var origins = new HashSet<string>(
            names.Keys.Where(id => !incoming.Contains(id)),
            StringComparer.OrdinalIgnoreCase);
        var destinations = new HashSet<string>(
            names.Keys.Where(id => !outgoing.Contains(id)),
            StringComparer.OrdinalIgnoreCase);
        var originName = JoinMetroTerminals(paths, origins, first: true, names);
        var destinationName = JoinMetroTerminals(paths, destinations, first: false, names);

        return new TransitDirectionResponse(
            direction,
            destinationName,
            originName,
            destinationName);
    }

    private static string? JoinMetroTerminals(
        IReadOnlyList<List<TdxMetroRouteStation>> paths,
        IReadOnlySet<string> candidates,
        bool first,
        IReadOnlyDictionary<string, string> names)
    {
        var ids = paths
            .Select(path => first ? path.First().StationID : path.Last().StationID)
            .Where(id => id is not null && candidates.Contains(id))
            .Cast<string>()
            .Concat(candidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var terminalNames = ids
            .Select(id => names.GetValueOrDefault(id))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return terminalNames.Count == 0 ? null : string.Join("／", terminalNames);
    }

    private static MetroStationResponse? MapMetroRouteStation(
        TdxMetroStationOfRoute route,
        TdxMetroRouteStation station,
        IReadOnlyDictionary<string, string> lineNames)
    {
        var lineId = route.LineID;
        var stationId = station.StationID;
        var nameZh = PreferredName(station.StationName);
        if (string.IsNullOrWhiteSpace(lineId) ||
            string.IsNullOrWhiteSpace(stationId) ||
            string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        return new MetroStationResponse(
            stationId,
            nameZh,
            station.StationName?.En,
            null,
            null,
            null,
            stationId,
            $"TDX:TRTC:{lineId}",
            FirstText(lineNames.GetValueOrDefault(lineId), lineId));
    }

    private static string? ParseMetroLineId(string routeId)
    {
        const string prefix = "TDX:TRTC:";
        if (!routeId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var lineId = routeId[prefix.Length..].Trim();
        return string.IsNullOrWhiteSpace(lineId) ? null : lineId;
    }

    private static bool AppliesToMetroLine(TdxMetroAlertScope? scope, string lineId) =>
        scope?.Lines is not { Count: > 0 } ||
        scope.Lines.Any(line => string.Equals(
            FirstText(line.LineID, line.LineNO),
            lineId,
            StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, IReadOnlyList<RailStationLinePositionResponse>>
        BuildRailStationPositions(IReadOnlyList<TdxTraStationOfLine> lines) =>
        lines
            .Where(line => !string.IsNullOrWhiteSpace(line.LineID))
            .SelectMany(line => line.Stations
                .Where(station => !string.IsNullOrWhiteSpace(station.StationID))
                .Select(station => new
                {
                    StationId = station.StationID!,
                    Position = new RailStationLinePositionResponse(
                        line.LineID!,
                        station.Sequence,
                        station.CumulativeDistance)
                }))
            .GroupBy(item => item.StationId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RailStationLinePositionResponse>)group
                    .Select(item => item.Position)
                    .GroupBy(
                        position => $"{position.LineId}:{position.Sequence}",
                        StringComparer.OrdinalIgnoreCase)
                    .Select(positionGroup => positionGroup.First())
                    .OrderBy(position => position.LineId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(position => position.Sequence)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

    private static RailStationResponse? MapRailStation(
        TdxTraStation station,
        IReadOnlyDictionary<string, IReadOnlyList<RailStationLinePositionResponse>> positionsByStation)
    {
        var id = station.StationID ?? station.StationUID;
        var nameZh = PreferredName(station.StationName);
        return string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh)
            ? null
            : new RailStationResponse(
                id,
                nameZh,
                station.StationName?.En,
                station.StationAddress,
                station.StationPosition?.PositionLat,
                station.StationPosition?.PositionLon,
                positionsByStation.GetValueOrDefault(id) ?? []);
    }

    private static IReadOnlyList<TransitArrivalResponse> BuildScheduledRailArrivals(
        string stationId,
        IReadOnlyList<TdxTraStationTimetable> timetables,
        DateTimeOffset? sourceUpdatedAt,
        DateTimeOffset now,
        IReadOnlyDictionary<string, RailStationResponse> stationsById)
    {
        var arrivals = new List<TransitArrivalResponse>();
        foreach (var timetable in timetables)
        {
            foreach (var entry in timetable.TimeTables.Where(item => item.SuspendedFlag != 1))
            {
                var scheduledAt = TdxTimeParser.ParseOccurrenceOnReferenceDate(
                    entry.DepartureTime ?? entry.ArrivalTime,
                    now);
                if (scheduledAt is null || scheduledAt < now.AddMinutes(-2))
                {
                    continue;
                }

                var trainNo = FirstText(entry.TrainNo);
                var responseStationId = FirstText(timetable.StationID, stationId)!;
                arrivals.Add(new TransitArrivalResponse(
                    $"rail-schedule:{responseStationId}:{trainNo ?? entry.Sequence.ToString(CultureInfo.InvariantCulture)}:{timetable.Direction}:{entry.Sequence}",
                    "rail",
                    responseStationId,
                    FirstText(PreferredName(timetable.StationName), responseStationId)!,
                    trainNo,
                    trainNo,
                    FirstText(entry.TrainTypeID, entry.TrainTypeCode),
                    FirstText(PreferredName(entry.TrainTypeName), entry.TrainTypeCode),
                    PreferredName(entry.DestinationStationName),
                    timetable.Direction,
                    scheduledAt,
                    null,
                    sourceUpdatedAt,
                    "表定班次",
                    false,
                    Heading: GetRailHeading(
                        responseStationId,
                        entry.DestinationStationID,
                        stationsById)));
            }
        }

        return arrivals;
    }

    private static DateTimeOffset? GetEndedServiceDayLastDeparture(
        IReadOnlyList<TdxTraStationTimetable> timetables,
        DateTimeOffset now)
    {
        var lastDepartureAt = timetables
            .SelectMany(item => item.TimeTables)
            .Where(item => item.SuspendedFlag != 1)
            .Select(item => TdxTimeParser.ParseOccurrenceOnReferenceDate(
                item.DepartureTime ?? item.ArrivalTime,
                now))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();
        return lastDepartureAt != default && lastDepartureAt < now
            ? lastDepartureAt
            : null;
    }

    private static TransitArrivalResponse? MapRailLiveArrival(
        TdxTraStationLiveBoard item,
        string requestedStationId,
        IReadOnlyList<TransitArrivalResponse> scheduled,
        IReadOnlyDictionary<string, RailStationResponse> stationsById,
        DateTimeOffset? feedUpdatedAt,
        DateTimeOffset now,
        int index)
    {
        var stationId = FirstText(item.StationID, requestedStationId);
        if (string.IsNullOrWhiteSpace(stationId))
        {
            return null;
        }

        var matchingSchedule = scheduled
            .Where(value => string.IsNullOrWhiteSpace(item.TrainNo) ||
                            string.Equals(
                                value.RouteName,
                                item.TrainNo,
                                StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value.ScheduledAt)
            .FirstOrDefault(value => value.ScheduledAt >= now.AddMinutes(-2));
        var scheduledAt = TdxTimeParser.ParseOccurrenceOnReferenceDate(
                              item.ScheduleArrivalTime ?? item.ScheduleDepartureTime,
                              now)
                          ?? matchingSchedule?.ScheduledAt;
        var delayMinutes = Math.Max(0, item.DelayTime);
        var estimatedAt = scheduledAt?.AddMinutes(delayMinutes);
        var stationName = FirstText(
            PreferredName(item.StationName),
            matchingSchedule?.StopName,
            stationId)!;
        var trainNo = FirstText(item.TrainNo, matchingSchedule?.RouteName);
        var sourceUpdatedAt = item.UpdateTime ?? feedUpdatedAt;

        return new TransitArrivalResponse(
            $"rail-live:{stationId}:{trainNo ?? index.ToString(CultureInfo.InvariantCulture)}:{item.Direction}:{index}",
            "rail",
            stationId,
            stationName,
            trainNo,
            trainNo,
            FirstText(item.TrainTypeID, item.TrainTypeCode, matchingSchedule?.LineId),
            FirstText(
                PreferredName(item.TrainTypeName),
                item.TrainTypeCode,
                matchingSchedule?.LineName),
            FirstText(
                PreferredName(item.EndingStationName),
                matchingSchedule?.DestinationName),
            item.Direction ?? matchingSchedule?.Direction,
            scheduledAt,
            estimatedAt,
            sourceUpdatedAt,
            GetRailServiceStatus(item.RunningStatus, item.DelayTime),
            false,
            item.Platform is "00" ? null : item.Platform,
            GetRailHeading(stationId, item.EndingStationID, stationsById)
                ?? matchingSchedule?.Heading);
    }

    private static IReadOnlyList<TransitArrivalResponse> SelectNextRailArrivals(
        IReadOnlyList<TransitArrivalResponse> scheduled,
        IReadOnlyList<TransitArrivalResponse> live,
        DateTimeOffset now)
    {
        var liveKeys = live
            .Select(GetRailArrivalKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return live
            .Concat(scheduled.Where(item => !liveKeys.Contains(GetRailArrivalKey(item))))
            .Where(item => (item.EstimatedAt ?? item.ScheduledAt) >= now.AddMinutes(-2))
            .GroupBy(GetRailArrivalKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .GroupBy(GetRailArrivalDirectionKey, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group
                .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
                .Take(10))
            .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
            .ToList();
    }

    private static string GetRailArrivalKey(TransitArrivalResponse arrival) =>
        string.IsNullOrWhiteSpace(arrival.RouteName)
            ? arrival.Id
            : $"{arrival.Direction?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}:{arrival.RouteName}";

    private static string GetRailArrivalDirectionKey(TransitArrivalResponse arrival) =>
        !string.IsNullOrWhiteSpace(arrival.Heading)
            ? arrival.Heading
            : $"direction:{arrival.Direction?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}";

    private static string? GetRailHeading(
        string stationId,
        string? destinationStationId,
        IReadOnlyDictionary<string, RailStationResponse> stationsById)
    {
        if (string.IsNullOrWhiteSpace(destinationStationId) ||
            !stationsById.TryGetValue(stationId, out var station) ||
            !stationsById.TryGetValue(destinationStationId, out var destination) ||
            station.Latitude is null ||
            destination.Latitude is null)
        {
            return null;
        }

        const double latitudeTolerance = 0.0001;
        var latitudeDifference = destination.Latitude.Value - station.Latitude.Value;
        if (Math.Abs(latitudeDifference) < latitudeTolerance)
        {
            return null;
        }

        return latitudeDifference > 0 ? "north" : "south";
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
                var scheduledAt = TdxTimeParser.ParseOccurrenceOnReferenceDate(
                    entry.DepartureTime ?? entry.ArrivalTime,
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

    private static DateTimeOffset? GetEndedMetroServiceDayLastDeparture(
        IReadOnlyList<TdxMetroStationTimetable> timetables,
        DateTimeOffset now)
    {
        var lastDepartureAt = timetables
            .Where(item => TdxTimeParser.IsServiceDay(item.ServiceDay, now))
            .SelectMany(item => item.Timetables)
            .Select(item => TdxTimeParser.ParseOccurrenceOnReferenceDate(
                item.DepartureTime ?? item.ArrivalTime,
                now))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();
        return lastDepartureAt != default && lastDepartureAt < now
            ? lastDepartureAt
            : null;
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

    private static string GetRailServiceStatus(int? status, int delayMinutes) => status switch
    {
        2 => "列車已取消",
        1 => delayMinutes > 0 ? $"誤點 {delayMinutes} 分" : "列車誤點",
        _ when delayMinutes > 0 => $"誤點 {delayMinutes} 分",
        0 => "準點",
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
