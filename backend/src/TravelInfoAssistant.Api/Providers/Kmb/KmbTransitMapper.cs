using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Kmb;

public static class KmbTransitMapper
{
    private static readonly TimeZoneInfo HongKongTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong");
    private static readonly TimeOnly ServiceDayBoundary = new(4, 0);

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(
        IReadOnlyList<KmbRouteRow> rows) =>
        rows
            .Where(item => item.ServiceType == "1" && Direction(item.Bound).HasValue)
            .GroupBy(item => item.Route, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var directions = group
                    .GroupBy(item => Direction(item.Bound)!.Value)
                    .Select(item => item.First())
                    .OrderBy(item => Direction(item.Bound))
                    .Select(item => new TransitDirectionResponse(
                        Direction(item.Bound)!.Value,
                        item.DestinationZh,
                        item.OriginZh,
                        item.DestinationZh))
                    .ToList();
                var outbound = group.FirstOrDefault(item => Direction(item.Bound) == 0) ?? group.First();
                return new TransitRouteResponse(
                    group.Key,
                    group.Key,
                    group.Key,
                    outbound.OriginZh,
                    outbound.DestinationZh,
                    ["九巴／龍運"],
                    directions,
                    []);
            })
            .OrderBy(item => NaturalRouteKey(item.NameZh), StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<TransitStopResponse> MapStops(
        string route,
        int direction,
        IReadOnlyList<KmbRouteStopRow> routeStops,
        IReadOnlyList<KmbStopRow> stops)
    {
        var stopLookup = stops.ToDictionary(item => item.StopId, StringComparer.OrdinalIgnoreCase);
        var bound = direction == 0 ? "O" : "I";
        return routeStops
            .Where(item =>
                item.Route.Equals(route, StringComparison.OrdinalIgnoreCase) &&
                item.ServiceType == "1" &&
                item.Bound.Equals(bound, StringComparison.OrdinalIgnoreCase))
            .Select(item => (Row: item, Sequence: ParseInt(item.Sequence)))
            .Where(item => item.Sequence.HasValue && stopLookup.ContainsKey(item.Row.StopId))
            .OrderBy(item => item.Sequence)
            .Select(item =>
            {
                var stop = stopLookup[item.Row.StopId];
                return new TransitStopResponse(
                    stop.StopId,
                    stop.NameZh,
                    stop.NameEn,
                    item.Sequence!.Value,
                    direction,
                    ParseDouble(stop.Latitude),
                    ParseDouble(stop.Longitude));
            })
            .ToList();
    }

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string route,
        int direction,
        string stopId,
        string stopName,
        IReadOnlyList<KmbEtaRow> rows)
    {
        var bound = direction == 0 ? "O" : "I";
        return rows
            .Where(item =>
                item.Route.Equals(route, StringComparison.OrdinalIgnoreCase) &&
                item.Direction.Equals(bound, StringComparison.OrdinalIgnoreCase) &&
                item.ServiceType == 1 &&
                item.EstimatedAt.HasValue)
            .OrderBy(item => item.EstimatedAt)
            .Take(3)
            .Select(item => new TransitArrivalResponse(
                $"KMB:{route}:{bound}:{stopId}:{item.EtaSequence}:{item.EstimatedAt:O}",
                "bus",
                stopId,
                stopName,
                route,
                route,
                null,
                null,
                item.DestinationZh,
                direction,
                item.EstimatedAt,
                item.EstimatedAt,
                item.DataTimestamp,
                string.IsNullOrWhiteSpace(item.RemarkZh) ? "即時預估" : item.RemarkZh,
                false))
            .ToList();
    }

    public static DateTimeOffset? FindLastOriginDeparture(
        string route,
        int direction,
        DateTimeOffset now,
        KmbGtfsSchedule schedule)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, HongKongTimeZone);
        var serviceDate = DateOnly.FromDateTime(localNow.DateTime);
        if (TimeOnly.FromDateTime(localNow.DateTime) < ServiceDayBoundary)
        {
            serviceDate = serviceDate.AddDays(-1);
        }
        var routeIds = schedule.Routes
            .Where(item => item.ShortName.Equals(route, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trips = schedule.Trips
            .Where(item =>
                routeIds.Contains(item.RouteId) &&
                item.Direction == direction &&
                IsServiceActive(item.ServiceId, serviceDate, schedule))
            .ToList();
        if (trips.Count == 0) return null;

        var tripIds = trips.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = schedule.Frequencies
            .Where(item => tripIds.Contains(item.TripId))
            .Select(item => LastFrequencyDeparture(item, serviceDate))
            .Concat(schedule.StopTimes
                .Where(item => tripIds.Contains(item.TripId))
                .GroupBy(item => item.TripId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(item => item.Sequence).First().DepartureTime)
                .Select(item => ParseServiceTime(item, serviceDate)))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        return candidates.Count == 0 ? null : candidates.Max();
    }

    private static DateTimeOffset? LastFrequencyDeparture(
        KmbGtfsFrequency frequency,
        DateOnly serviceDate)
    {
        var start = ParseServiceTime(frequency.StartTime, serviceDate);
        var end = ParseServiceTime(frequency.EndTime, serviceDate);
        if (!start.HasValue || !end.HasValue || frequency.HeadwaySeconds <= 0 || end <= start)
        {
            return null;
        }

        var serviceSeconds = (end.Value - start.Value).TotalSeconds;
        var intervals = Math.Max(0, (int)Math.Ceiling(serviceSeconds / frequency.HeadwaySeconds) - 1);
        return start.Value.AddSeconds(intervals * frequency.HeadwaySeconds);
    }

    private static bool IsServiceActive(
        string serviceId,
        DateOnly date,
        KmbGtfsSchedule schedule)
    {
        var exception = schedule.CalendarDates.LastOrDefault(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) && item.Date == date);
        if (exception is not null) return exception.ExceptionType == 1;
        return schedule.Calendars.Any(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) &&
            date >= item.StartDate && date <= item.EndDate && item.Days.Contains(date.DayOfWeek));
    }

    private static DateTimeOffset? ParseServiceTime(string? value, DateOnly serviceDate)
    {
        var parts = value?.Split(':');
        if (parts is not { Length: >= 2 } ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }

        var local = serviceDate.ToDateTime(TimeOnly.MinValue).AddHours(hours).AddMinutes(minutes);
        return new DateTimeOffset(local, HongKongTimeZone.GetUtcOffset(local));
    }

    private static int? Direction(string value) => value.ToUpperInvariant() switch
    {
        "O" => 0,
        "I" => 1,
        _ => null
    };

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static string NaturalRouteKey(string value) => value.PadLeft(8, '0');
}
