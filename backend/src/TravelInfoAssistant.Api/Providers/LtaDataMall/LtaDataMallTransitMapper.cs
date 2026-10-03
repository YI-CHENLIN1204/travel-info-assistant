using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public static class LtaDataMallTransitMapper
{
    private const string Prefix = "LTA";
    private static readonly TimeZoneInfo SingaporeTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(LtaGtfsNetwork network)
    {
        var stops = StopLookup(network);
        return RouteGroups(network)
            .Select(group => MapRoute(group, network, stops))
            .Where(route => route is not null)
            .Cast<TransitRouteResponse>()
            .OrderBy(route => route.NameEn ?? route.NameZh, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<MetroStationResponse> MapStations(LtaGtfsNetwork network)
    {
        var stops = StopLookup(network);
        return RouteGroups(network)
            .SelectMany(group =>
            {
                var sourceRouteIds = SourceRouteIds(group);
                return network.RouteStops
                    .Where(item =>
                        sourceRouteIds.Contains(item.RouteId) &&
                        stops.ContainsKey(item.StopId))
                    .Select(item => CanonicalStopId(item.StopId, stops))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(stopId => MapStation(group, stopId, network, stops));
            })
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
        var group = FindRouteGroup(routeId, network);
        if (group is null || !stops.ContainsKey(stationStopId))
        {
            return [];
        }

        var canonicalStationId = CanonicalStopId(stationStopId, stops);
        var canonicalStation = stops[canonicalStationId];
        var sourceRouteIds = SourceRouteIds(group);
        var trips = network.Trips
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => item.First(),
                StringComparer.OrdinalIgnoreCase);
        var updatesByTrip = feed.TripUpdates
            .Where(item => !string.IsNullOrWhiteSpace(item.TripId))
            .GroupBy(item => item.TripId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => item.First(),
                StringComparer.OrdinalIgnoreCase);
        var lineName = LineNameZh(group.Primary, network);
        var results = new List<TransitArrivalResponse>();
        var representedUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in ScheduledCandidates(
                     group,
                     canonicalStationId,
                     network,
                     stops,
                     trips,
                     now))
        {
            updatesByTrip.TryGetValue(candidate.Trip.Id, out var matchingUpdate);
            var stopUpdate = matchingUpdate?.StopTimeUpdates.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.StopId) &&
                CanonicalStopId(item.StopId, stops)
                    .Equals(canonicalStationId, StringComparison.OrdinalIgnoreCase));
            var update = stopUpdate is not null || matchingUpdate?.ScheduleRelationship == 3
                ? matchingUpdate
                : null;
            var delaySeconds = stopUpdate?.DepartureDelaySeconds ??
                               stopUpdate?.ArrivalDelaySeconds;
            var estimatedAt = stopUpdate?.DepartureTime ?? stopUpdate?.ArrivalTime;
            if (!estimatedAt.HasValue && delaySeconds.HasValue)
            {
                estimatedAt = candidate.ScheduledAt.AddSeconds(delaySeconds.Value);
            }

            var displayAt = estimatedAt ?? candidate.ScheduledAt;
            if (displayAt < now.AddMinutes(-1) || displayAt > now.AddHours(4))
            {
                continue;
            }

            if (update is not null)
            {
                representedUpdates.Add(update.Id);
            }

            var exactStop = ResolveExactStop(stopUpdate?.StopId ?? candidate.StopTime.StopId, stops)
                ?? canonicalStation;
            results.Add(new TransitArrivalResponse(
                update is null
                    ? $"LTA:scheduled:{candidate.Trip.Id}:{candidate.StopTime.StopId}:{candidate.ScheduledAt.ToUnixTimeSeconds()}"
                    : $"LTA:{update.Id}:{candidate.StopTime.StopId}:{displayAt.ToUnixTimeSeconds()}",
                "metro",
                StationId(group.Primary.Id, canonicalStationId),
                StationNameZh(canonicalStation, network),
                RouteId(group.Primary.Id),
                lineName,
                RouteId(group.Primary.Id),
                lineName,
                candidate.Trip.Headsign ??
                DirectionDestination(group, candidate.Trip.DirectionId, network, stops),
                update?.DirectionId ?? candidate.Trip.DirectionId,
                candidate.ScheduledAt,
                update is null ? null : estimatedAt ?? candidate.ScheduledAt,
                update is null ? null : feed.Timestamp ?? update.Timestamp,
                update is null
                    ? "表定時間"
                    : ServiceStatus(
                        update.ScheduleRelationship,
                        stopUpdate?.ScheduleRelationship ?? 0,
                        delaySeconds),
                false,
                exactStop.PlatformCode));
        }

        foreach (var update in feed.TripUpdates.Where(item => !representedUpdates.Contains(item.Id)))
        {
            trips.TryGetValue(update.TripId ?? string.Empty, out var trip);
            var updateRouteId = update.RouteId ?? trip?.RouteId;
            if (string.IsNullOrWhiteSpace(updateRouteId) || !sourceRouteIds.Contains(updateRouteId))
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
            var exactStop = ResolveExactStop(stopUpdate.StopId, stops) ?? canonicalStation;
            results.Add(new TransitArrivalResponse(
                $"LTA:{update.Id}:{stopUpdate.StopId}:{estimatedAt.Value.ToUnixTimeSeconds()}",
                "metro",
                StationId(group.Primary.Id, canonicalStationId),
                StationNameZh(canonicalStation, network),
                RouteId(group.Primary.Id),
                lineName,
                RouteId(group.Primary.Id),
                lineName,
                trip?.Headsign ?? DirectionDestination(group, direction, network, stops),
                direction,
                null,
                estimatedAt,
                feed.Timestamp ?? update.Timestamp,
                ServiceStatus(
                    update.ScheduleRelationship,
                    stopUpdate.ScheduleRelationship,
                    delaySeconds),
                false,
                exactStop.PlatformCode));
        }

        return results
            .GroupBy(item => new
            {
                Direction = item.Direction ?? 0,
                Time = (item.EstimatedAt ?? item.ScheduledAt)?.ToUnixTimeSeconds(),
                item.DestinationName
            })
            .Select(item => item.OrderByDescending(value => value.EstimatedAt.HasValue).First())
            .GroupBy(item => item.Direction ?? 0)
            .SelectMany(grouped => grouped
                .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
                .Take(4))
            .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
            .ToList();
    }

    public static DateTimeOffset? GetEndedServiceDayLastDeparture(
        string routeId,
        string stationStopId,
        LtaGtfsNetwork network,
        DateTimeOffset now)
    {
        var stops = StopLookup(network);
        var group = FindRouteGroup(routeId, network);
        if (group is null || !stops.ContainsKey(stationStopId))
        {
            return null;
        }

        var canonicalStationId = CanonicalStopId(stationStopId, stops);
        var sourceRouteIds = SourceRouteIds(group);
        var trips = network.Trips
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => item.First(),
                StringComparer.OrdinalIgnoreCase);
        var localNow = TimeZoneInfo.ConvertTime(now, SingaporeTimeZone);
        var currentServiceDate = DateOnly.FromDateTime(localNow.DateTime);
        var serviceDates = new[]
        {
            currentServiceDate.AddDays(-1),
            currentServiceDate
        };
        var scheduledDepartures = new List<DateTimeOffset>();

        foreach (var stopTime in network.StopTimes ?? [])
        {
            if (!trips.TryGetValue(stopTime.TripId, out var trip) ||
                !sourceRouteIds.Contains(trip.RouteId) ||
                !CanonicalStopId(stopTime.StopId, stops)
                    .Equals(canonicalStationId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var serviceDate in serviceDates)
            {
                if (!IsServiceActive(trip.ServiceId, serviceDate, network) ||
                    !TryParseGtfsTime(
                        serviceDate,
                        stopTime.DepartureTime ?? stopTime.ArrivalTime,
                        out var scheduledAt))
                {
                    continue;
                }

                var localScheduledAt = TimeZoneInfo.ConvertTime(scheduledAt, SingaporeTimeZone);
                if (DateOnly.FromDateTime(localScheduledAt.DateTime) < currentServiceDate)
                {
                    continue;
                }

                scheduledDepartures.Add(scheduledAt);
            }
        }

        if (scheduledDepartures.Count == 0 || scheduledDepartures.Any(item => item >= now))
        {
            return null;
        }

        return scheduledDepartures.Max();
    }

    public static MetroServiceStatusResponse? MapStatus(
        string routeId,
        LtaRealtimeFeed feed,
        LtaGtfsNetwork network,
        DateTimeOffset now)
    {
        var group = FindRouteGroup(routeId, network);
        if (group is null)
        {
            return null;
        }

        var sourceRouteIds = SourceRouteIds(group);
        var routeStopIds = network.RouteStops
            .Where(item => sourceRouteIds.Contains(item.RouteId))
            .Select(item => item.StopId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var alerts = feed.Alerts
            .Where(alert =>
                (!alert.ActiveFrom.HasValue || alert.ActiveFrom <= now) &&
                (!alert.ActiveUntil.HasValue || alert.ActiveUntil >= now))
            .Where(alert =>
                alert.RouteIds.Count == 0 && alert.StopIds.Count == 0 ||
                alert.RouteIds.Any(sourceRouteIds.Contains) ||
                alert.StopIds.Any(routeStopIds.Contains))
            .ToList();
        var updatedAt = feed.Timestamp;
        if (alerts.Count == 0)
        {
            return new MetroServiceStatusResponse(
                $"LTA:Status:{group.Primary.Id}",
                RouteId(group.Primary.Id),
                LineNameZh(group.Primary, network),
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
            $"LTA:Status:{group.Primary.Id}",
            RouteId(group.Primary.Id),
            LineNameZh(group.Primary, network),
            null,
            string.Join(" ", messages),
            updatedAt,
            updatedAt?.AddSeconds(90),
            EffectLabel(alerts[0].Effect));
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

    private static IReadOnlyList<ScheduledCandidate> ScheduledCandidates(
        RouteGroup group,
        string canonicalStationId,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops,
        IReadOnlyDictionary<string, LtaGtfsTrip> trips,
        DateTimeOffset now)
    {
        var stopTimes = network.StopTimes ?? [];
        if (stopTimes.Count == 0)
        {
            return [];
        }

        var sourceRouteIds = SourceRouteIds(group);
        var localNow = TimeZoneInfo.ConvertTime(now, SingaporeTimeZone);
        var serviceDates = new[]
        {
            DateOnly.FromDateTime(localNow.DateTime).AddDays(-1),
            DateOnly.FromDateTime(localNow.DateTime),
            DateOnly.FromDateTime(localNow.DateTime).AddDays(1)
        };
        var results = new List<ScheduledCandidate>();
        foreach (var stopTime in stopTimes)
        {
            if (!trips.TryGetValue(stopTime.TripId, out var trip) ||
                !sourceRouteIds.Contains(trip.RouteId) ||
                !CanonicalStopId(stopTime.StopId, stops)
                    .Equals(canonicalStationId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timeText = stopTime.DepartureTime ?? stopTime.ArrivalTime;
            foreach (var serviceDate in serviceDates)
            {
                if (!IsServiceActive(trip.ServiceId, serviceDate, network) ||
                    !TryParseGtfsTime(serviceDate, timeText, out var scheduledAt) ||
                    scheduledAt < now.AddMinutes(-1) ||
                    scheduledAt > now.AddHours(4))
                {
                    continue;
                }

                results.Add(new ScheduledCandidate(trip, stopTime, scheduledAt));
            }
        }

        return results;
    }

    private static bool IsServiceActive(
        string? serviceId,
        DateOnly date,
        LtaGtfsNetwork network)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return true;
        }

        var calendarDates = network.CalendarDates ?? [];
        var exception = calendarDates.LastOrDefault(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) &&
            item.Date == date);
        if (exception is not null)
        {
            return exception.ExceptionType == 1;
        }

        var calendars = network.Calendars ?? [];
        var calendar = calendars.FirstOrDefault(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
        if (calendar is not null)
        {
            return date >= calendar.StartDate &&
                   date <= calendar.EndDate &&
                   calendar.Days.Contains(date.DayOfWeek);
        }

        var serviceHasExplicitDates = calendarDates.Any(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
        return calendars.Count == 0 && !serviceHasExplicitDates;
    }

    private static bool TryParseGtfsTime(
        DateOnly serviceDate,
        string? value,
        out DateTimeOffset result)
    {
        result = default;
        var parts = value?.Split(':');
        if (parts is not { Length: 2 or 3 } ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            (parts.Length > 2 &&
             !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _)) ||
            hours < 0 ||
            minutes is < 0 or > 59)
        {
            return false;
        }

        var seconds = parts.Length > 2
            ? int.Parse(parts[2], CultureInfo.InvariantCulture)
            : 0;
        if (seconds is < 0 or > 59)
        {
            return false;
        }

        var local = serviceDate
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified)
            .AddHours(hours)
            .AddMinutes(minutes)
            .AddSeconds(seconds);
        result = new DateTimeOffset(local, SingaporeTimeZone.GetUtcOffset(local));
        return true;
    }

    private static TransitRouteResponse? MapRoute(
        RouteGroup group,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        var sourceRouteIds = SourceRouteIds(group);
        var directions = network.RouteStops
            .Where(item => sourceRouteIds.Contains(item.RouteId))
            .GroupBy(item => new { item.RouteId, item.DirectionId })
            .Select(pattern => new
            {
                pattern.Key.DirectionId,
                Stops = pattern
                    .OrderBy(item => item.Sequence)
                    .Select(item => CanonicalStopId(item.StopId, stops))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(stops.ContainsKey)
                    .ToList()
            })
            .Where(pattern => pattern.Stops.Count > 0)
            .GroupBy(pattern => pattern.DirectionId)
            .Select(patterns => patterns.OrderByDescending(item => item.Stops.Count).First())
            .OrderBy(item => item.DirectionId)
            .ToList();
        var primary = directions.OrderByDescending(item => item.Stops.Count).FirstOrDefault()?.Stops;
        if (primary is null)
        {
            return null;
        }

        var allStations = primary.ToList();
        allStations.AddRange(network.RouteStops
            .Where(item => sourceRouteIds.Contains(item.RouteId))
            .Select(item => CanonicalStopId(item.StopId, stops))
            .Where(stops.ContainsKey)
            .Where(stopId => !allStations.Contains(stopId, StringComparer.OrdinalIgnoreCase)));
        var origin = StationNameZh(stops[primary.First()], network);
        var destination = StationNameZh(stops[primary.Last()], network);
        var mappedDirections = directions
            .Select(direction =>
            {
                var directionOrigin = StationNameZh(stops[direction.Stops.First()], network);
                var directionDestination = StationNameZh(stops[direction.Stops.Last()], network);
                return new TransitDirectionResponse(
                    direction.DirectionId,
                    directionDestination,
                    directionOrigin,
                    directionDestination);
            })
            .ToList();

        return new TransitRouteResponse(
            RouteId(group.Primary.Id),
            LineNameZh(group.Primary, network),
            LineNameEn(group.Primary),
            origin,
            destination,
            group.Routes
                .Select(route => string.IsNullOrWhiteSpace(route.AgencyId) ? "LTA" : route.AgencyId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            mappedDirections,
            allStations.Select(stopId => StationNameZh(stops[stopId], network)).ToList());
    }

    private static MetroStationResponse? MapStation(
        RouteGroup group,
        string stopId,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        if (!stops.TryGetValue(stopId, out var stop))
        {
            return null;
        }

        return new MetroStationResponse(
            StationId(group.Primary.Id, stopId),
            StationNameZh(stop, network),
            stop.Name,
            null,
            stop.Latitude,
            stop.Longitude,
            stop.Code,
            RouteId(group.Primary.Id),
            LineNameZh(group.Primary, network));
    }

    private static string DirectionDestination(
        RouteGroup group,
        int direction,
        LtaGtfsNetwork network,
        IReadOnlyDictionary<string, LtaGtfsStop> stops)
    {
        var sourceRouteIds = SourceRouteIds(group);
        var stopId = network.RouteStops
            .Where(item =>
                sourceRouteIds.Contains(item.RouteId) &&
                item.DirectionId == direction)
            .GroupBy(item => item.RouteId, StringComparer.OrdinalIgnoreCase)
            .Select(pattern => pattern.OrderBy(item => item.Sequence).ToList())
            .OrderByDescending(pattern => pattern.Count)
            .Select(pattern => pattern.LastOrDefault())
            .Where(item => item is not null)
            .Select(item => CanonicalStopId(item!.StopId, stops))
            .FirstOrDefault(stops.ContainsKey);
        return stopId is null ? "終點站" : StationNameZh(stops[stopId], network);
    }

    private static IReadOnlyList<RouteGroup> RouteGroups(LtaGtfsNetwork network) =>
        network.Routes
            .Where(route => network.RouteStops.Any(item =>
                item.RouteId.Equals(route.Id, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(PublicRouteKey, StringComparer.OrdinalIgnoreCase)
            .Select(routes =>
            {
                var values = routes.ToList();
                var primary = values
                    .OrderByDescending(route => network.RouteStops
                        .Where(item => item.RouteId.Equals(route.Id, StringComparison.OrdinalIgnoreCase))
                        .Select(item => item.StopId)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count())
                    .ThenBy(route => route.Id.Contains('_') ? 1 : 0)
                    .ThenBy(route => route.Id.Length)
                    .ThenBy(route => route.Id, StringComparer.OrdinalIgnoreCase)
                    .First();
                return new RouteGroup(primary, values);
            })
            .ToList();

    private static RouteGroup? FindRouteGroup(string routeId, LtaGtfsNetwork network) =>
        RouteGroups(network).FirstOrDefault(group =>
            group.Primary.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase) ||
            group.Routes.Any(route => route.Id.Equals(routeId, StringComparison.OrdinalIgnoreCase)));

    private static HashSet<string> SourceRouteIds(RouteGroup group) =>
        group.Routes
            .Select(route => route.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string PublicRouteKey(LtaGtfsRoute route) =>
        string.IsNullOrWhiteSpace(route.LongName) ? route.ShortName : route.LongName;

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

    private static LtaGtfsStop? ResolveExactStop(
        string? stopId,
        IReadOnlyDictionary<string, LtaGtfsStop> stops) =>
        !string.IsNullOrWhiteSpace(stopId) && stops.TryGetValue(stopId, out var stop)
            ? stop
            : null;

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

    private sealed record RouteGroup(
        LtaGtfsRoute Primary,
        IReadOnlyList<LtaGtfsRoute> Routes);

    private sealed record ScheduledCandidate(
        LtaGtfsTrip Trip,
        LtaGtfsStopTime StopTime,
        DateTimeOffset ScheduledAt);
}
