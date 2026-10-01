using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public static class LtaDataMallTransitMapper
{
    private const string Prefix = "LTA";

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(LtaGtfsNetwork network)
    {
        var stops = StopLookup(network);
        return network.Routes
            .Where(route => network.RouteStops.Any(item =>
                item.RouteId.Equals(route.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(route => MapRoute(route, network, stops))
            .Where(route => route is not null)
            .Cast<TransitRouteResponse>()
            .OrderBy(route => route.NameEn ?? route.NameZh, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<MetroStationResponse> MapStations(LtaGtfsNetwork network)
    {
        var stops = StopLookup(network);
        return network.RouteStops
            .Where(item => stops.ContainsKey(item.StopId))
            .Select(item => new
            {
                item.RouteId,
                StopId = CanonicalStopId(item.StopId, stops)
            })
            .GroupBy(
                item => $"{item.RouteId}\u001f{item.StopId}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => MapStation(item.RouteId, item.StopId, network, stops))
            .Where(station => station is not null)
            .Cast<MetroStationResponse>()
            .OrderBy(station => station.RailwayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(station => station.NameEn, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string routeId,
        string stationStopId,
        LtaRealtimeFeed feed,
        LtaGtfsNetwork network,
        DateTimeOffset now)
    {
        var stops = StopLookup(network);
        if (!stops.ContainsKey(stationStopId))
        {
            return [];
        }

        var canonicalStationId = CanonicalStopId(stationStopId, stops);
        var canonicalStation = stops[canonicalStationId];
        var route = network.Routes.FirstOrDefault(item =>
            item.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return [];
        }

        var trips = network.Trips.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var lineName = LineNameZh(route, network);
        var results = new List<TransitArrivalResponse>();
        foreach (var update in feed.TripUpdates)
        {
            trips.TryGetValue(update.TripId ?? string.Empty, out var trip);
            var updateRouteId = update.RouteId ?? trip?.RouteId;
            if (!routeId.Equals(updateRouteId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var stopUpdate = update.StopTimeUpdates.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.StopId) &&
                CanonicalStopId(item.StopId, stops)
                    .Equals(canonicalStationId, StringComparison.OrdinalIgnoreCase));
            if (stopUpdate is null)
            {
                continue;
            }

            var estimatedAt = stopUpdate.DepartureTime ?? stopUpdate.ArrivalTime;
            if (!estimatedAt.HasValue ||
                estimatedAt.Value < now.AddMinutes(-1) ||
                estimatedAt.Value > now.AddHours(4))
            {
                continue;
            }

            var direction = update.DirectionId ?? trip?.DirectionId ?? 0;
            var delaySeconds = stopUpdate.DepartureDelaySeconds ??
                               stopUpdate.ArrivalDelaySeconds;
            var exactStop = !string.IsNullOrWhiteSpace(stopUpdate.StopId) &&
                            stops.TryGetValue(stopUpdate.StopId, out var resolvedStop)
                ? resolvedStop
                : canonicalStation;
            results.Add(new TransitArrivalResponse(
                $"LTA:{update.Id}:{stopUpdate.StopId}:{estimatedAt.Value.ToUnixTimeSeconds()}",
                "metro",
                StationId(routeId, canonicalStationId),
                StationNameZh(canonicalStation, network),
                RouteId(routeId),
                lineName,
                RouteId(routeId),
                lineName,
                trip?.Headsign ?? DirectionDestination(routeId, direction, network, stops),
                direction,
                null,
                estimatedAt,
                feed.Timestamp ?? update.Timestamp,
                ServiceStatus(update.ScheduleRelationship, stopUpdate.ScheduleRelationship, delaySeconds),
                false,
                exactStop.PlatformCode));
        }

        return results
            .GroupBy(item => item.Direction ?? 0)
            .SelectMany(group => group.OrderBy(item => item.EstimatedAt).Take(4))
            .OrderBy(item => item.EstimatedAt)
            .ToList();
    }

    public static MetroServiceStatusResponse? MapStatus(
        string routeId,
        LtaRealtimeFeed feed,
        LtaGtfsNetwork network,
        DateTimeOffset now)
    {
        var route = network.Routes.FirstOrDefault(item =>
            item.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return null;
        }

        var routeStopIds = network.RouteStops
            .Where(item => item.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.StopId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alerts = feed.Alerts
            .Where(alert =>
                (!alert.ActiveFrom.HasValue || alert.ActiveFrom <= now) &&
                (!alert.ActiveUntil.HasValue || alert.ActiveUntil >= now))
            .Where(alert =>
                alert.RouteIds.Count == 0 && alert.StopIds.Count == 0 ||
                alert.RouteIds.Contains(routeId, StringComparer.OrdinalIgnoreCase) ||
                alert.StopIds.Any(routeStopIds.Contains))
            .ToList();
        var updatedAt = feed.Timestamp;
        if (alerts.Count == 0)
        {
            return new MetroServiceStatusResponse(
                $"LTA:Status:{routeId}",
                RouteId(routeId),
                LineNameZh(route, network),
                null,
                null,
                updatedAt,
                updatedAt?.AddSeconds(90),
                "目前沒有此路線的官方服務警示。");
        }

        var messages = alerts
            .Select(alert =>
            {
                var detail = string.Join(" ", new[] { alert.Header, alert.Description }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
                var effect = EffectLabel(alert.Effect);
                return string.IsNullOrWhiteSpace(detail) ? effect : $"{effect}：{detail}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();
        return new MetroServiceStatusResponse(
            $"LTA:Status:{routeId}",
            RouteId(routeId),
            LineNameZh(route, network),
            null,
            string.Join(" ", messages),
            updatedAt,
            updatedAt?.AddSeconds(90));
    }

    public static bool TryParseRouteId(string value, out string routeId)
    {
        var parts = value.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0].Equals(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            routeId = parts[1];
            return true;
        }

        routeId = string.Empty;
        return false;
    }

    public static bool TryParseStationId(
        string value,
        out string routeId,
        out string stopId)
    {
        var parts = value.Split(':', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[0].Equals(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            routeId = parts[1];
            stopId = parts[2];
            return true;
        }

        routeId = string.Empty;
        stopId = string.Empty;
        return false;
    }

    private static TransitRouteResponse? MapRoute(
        LtaGtfsRoute route,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        var directions = network.RouteStops
            .Where(item => item.RouteId.Equals(route.Id, StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.DirectionId)
            .Select(group => new
            {
                Direction = group.Key,
                Stops = group
                    .OrderBy(item => item.Sequence)
                    .Select(item => CanonicalStopId(item.StopId, stops))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(stops.ContainsKey)
                    .ToList()
            })
            .Where(direction => direction.Stops.Count > 0)
            .OrderByDescending(direction => direction.Stops.Count)
            .ToList();
        var primary = directions.FirstOrDefault()?.Stops;
        if (primary is null)
        {
            return null;
        }

        var allStations = primary.ToList();
        allStations.AddRange(directions
            .SelectMany(direction => direction.Stops)
            .Where(stopId => !allStations.Contains(stopId, StringComparer.OrdinalIgnoreCase)));
        var origin = StationNameZh(stops[primary.First()], network);
        var destination = StationNameZh(stops[primary.Last()], network);
        var mappedDirections = directions
            .Select(direction =>
            {
                var directionOrigin = StationNameZh(stops[direction.Stops.First()], network);
                var directionDestination = StationNameZh(stops[direction.Stops.Last()], network);
                return new TransitDirectionResponse(
                    direction.Direction,
                    directionDestination,
                    directionOrigin,
                    directionDestination);
            })
            .ToList();

        return new TransitRouteResponse(
            RouteId(route.Id),
            LineNameZh(route, network),
            LineNameEn(route),
            origin,
            destination,
            [string.IsNullOrWhiteSpace(route.AgencyId) ? "LTA" : route.AgencyId],
            mappedDirections,
            allStations.Select(stopId => StationNameZh(stops[stopId], network)).ToList());
    }

    private static MetroStationResponse? MapStation(
        string routeId,
        string stopId,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        if (!stops.TryGetValue(stopId, out var stop))
        {
            return null;
        }

        var route = network.Routes.FirstOrDefault(item =>
            item.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return null;
        }

        return new MetroStationResponse(
            StationId(routeId, stopId),
            StationNameZh(stop, network),
            stop.Name,
            null,
            stop.Latitude,
            stop.Longitude,
            stop.Code,
            RouteId(routeId),
            LineNameZh(route, network));
    }

    private static string DirectionDestination(
        string routeId,
        int direction,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        var stopId = network.RouteStops
            .Where(item =>
                item.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase) &&
                item.DirectionId == direction)
            .OrderByDescending(item => item.Sequence)
            .Select(item => CanonicalStopId(item.StopId, stops))
            .FirstOrDefault(stops.ContainsKey);
        return stopId is null ? "終點站" : StationNameZh(stops[stopId], network);
    }

    private static string ServiceStatus(
        int tripRelationship,
        int stopRelationship,
        int? delaySeconds)
    {
        if (tripRelationship == 3) return "列車取消";
        if (stopRelationship == 1) return "不停靠本站";
        if (delaySeconds >= 60) return $"延誤 {Math.Ceiling(delaySeconds.Value / 60d):0} 分鐘";
        return "即時預估";
    }

    private static string EffectLabel(int effect) => effect switch
    {
        1 => "服務中斷",
        2 => "班次減少",
        3 => "嚴重延誤",
        4 => "路線改道",
        5 => "加開服務",
        6 => "服務調整",
        9 => "停靠站異動",
        11 => "無障礙設施異常",
        _ => "官方服務警示"
    };

    private static IReadOnlyDictionary<string, LtaGtfsStop> StopLookup(LtaGtfsNetwork network) =>
        network.Stops
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private static string CanonicalStopId(
        string stopId,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        if (!stops.TryGetValue(stopId, out var stop) ||
            string.IsNullOrWhiteSpace(stop.ParentStation) ||
            !stops.ContainsKey(stop.ParentStation))
        {
            return stopId;
        }
        return stop.ParentStation;
    }

    private static string StationNameZh(LtaGtfsStop stop, LtaGtfsNetwork network) =>
        network.ChineseNames.FirstOrDefault(item =>
            item.StationCode.Equals(stop.Code, StringComparison.OrdinalIgnoreCase))?.NameZh ??
        stop.Name;

    private static string LineNameZh(LtaGtfsRoute route, LtaGtfsNetwork network) =>
        network.ChineseNames.FirstOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.LineNameEn) &&
            item.LineNameEn.Equals(route.LongName, StringComparison.OrdinalIgnoreCase))?.LineNameZh ??
        LineNameEn(route);

    private static string LineNameEn(LtaGtfsRoute route) =>
        string.IsNullOrWhiteSpace(route.LongName) ? route.ShortName : route.LongName;

    private static string RouteId(string routeId) => $"{Prefix}:{routeId}";
    private static string StationId(string routeId, string stopId) =>
        $"{Prefix}:{routeId}:{stopId}";
}
