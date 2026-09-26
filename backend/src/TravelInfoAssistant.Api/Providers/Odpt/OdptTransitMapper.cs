using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public static class OdptTransitMapper
{
    private static readonly TimeZoneInfo TokyoTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");

    private static readonly IReadOnlyDictionary<string, string> RailwayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ginza"] = "銀座線",
            ["Marunouchi"] = "丸ノ内線",
            ["Hibiya"] = "日比谷線",
            ["Tozai"] = "東西線",
            ["Chiyoda"] = "千代田線",
            ["Yurakucho"] = "有楽町線",
            ["Hanzomon"] = "半蔵門線",
            ["Namboku"] = "南北線",
            ["Fukutoshin"] = "副都心線"
        };

    public static TransitRouteResponse? MapRailway(OdptRailway railway)
    {
        var id = railway.SameAs?.Trim();
        var nameZh = railway.RailwayTitle?.Ja?.Trim() ?? railway.Title?.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        var stations = railway.StationOrder
            .OrderBy(item => item.Index)
            .Where(item => !string.IsNullOrWhiteSpace(item.StationTitle?.Ja))
            .ToList();
        var origin = stations.FirstOrDefault()?.StationTitle?.Ja;
        var destination = stations.LastOrDefault()?.StationTitle?.Ja;
        var directions = new List<TransitDirectionResponse>();
        if (!string.IsNullOrWhiteSpace(origin) || !string.IsNullOrWhiteSpace(destination))
        {
            directions.Add(new TransitDirectionResponse(0, destination, origin, destination));
            directions.Add(new TransitDirectionResponse(1, origin, destination, origin));
        }

        return new TransitRouteResponse(
            id,
            nameZh,
            railway.RailwayTitle?.En?.Trim(),
            origin,
            destination,
            ["Tokyo Metro"],
            directions);
    }

    public static MetroStationResponse? MapStation(OdptStation station)
    {
        var id = station.SameAs?.Trim();
        var nameZh = station.StationTitle?.Ja?.Trim() ?? station.Title?.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        return new MetroStationResponse(
            id,
            nameZh,
            station.StationTitle?.En?.Trim(),
            null,
            station.Latitude,
            station.Longitude,
            station.StationCode?.Trim(),
            station.Railway?.Trim(),
            GetRailwayName(station.Railway));
    }

    public static IReadOnlyList<TransitArrivalResponse> MapStationDepartures(
        IReadOnlyList<OdptStationTimetable> timetables,
        IReadOnlyList<OdptCalendar> calendars,
        IReadOnlyDictionary<string, string> stationNames,
        DateTimeOffset now)
    {
        var tokyoNow = TimeZoneInfo.ConvertTime(now, TokyoTimeZone);
        var serviceDate = DateOnly.FromDateTime(tokyoNow.DateTime);
        var applicableCalendars = SelectApplicableCalendars(
            timetables,
            calendars,
            serviceDate,
            tokyoNow.DayOfWeek);

        return timetables
            .Where(item => applicableCalendars.Count == 0 ||
                           string.IsNullOrWhiteSpace(item.Calendar) ||
                           applicableCalendars.Contains(item.Calendar))
            .SelectMany((timetable, timetableIndex) => timetable.Objects.Select(
                (item, itemIndex) => MapDeparture(
                    timetable,
                    item,
                    stationNames,
                    serviceDate,
                    timetableIndex,
                    itemIndex)))
            .Where(item => item is not null && item.ScheduledAt >= now)
            .Cast<TransitArrivalResponse>()
            .OrderBy(item => item.ScheduledAt)
            .Take(20)
            .ToList();
    }

    private static TransitArrivalResponse? MapDeparture(
        OdptStationTimetable timetable,
        OdptStationTimetableObject item,
        IReadOnlyDictionary<string, string> stationNames,
        DateOnly serviceDate,
        int timetableIndex,
        int itemIndex)
    {
        var timeText = item.DepartureTime?.Trim() ?? item.ArrivalTime?.Trim();
        if (!TimeOnly.TryParseExact(
                timeText,
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            return null;
        }

        var localDateTime = serviceDate.ToDateTime(time, DateTimeKind.Unspecified);
        var scheduledAt = new DateTimeOffset(
            localDateTime,
            TokyoTimeZone.GetUtcOffset(localDateTime));
        var stationId = timetable.Station?.Trim();
        if (string.IsNullOrWhiteSpace(stationId))
        {
            return null;
        }

        var destinations = item.DestinationStations
            .Select(id => GetStationName(id, stationNames))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var lineName = timetable.RailwayTitle?.Ja?.Trim() ?? GetRailwayName(timetable.Railway);
        var stopName = timetable.StationTitle?.Ja?.Trim() ??
                       GetStationName(stationId, stationNames);
        var platform = item.PlatformName?.Ja?.Trim() ?? item.PlatformNumber?.Trim();

        return new TransitArrivalResponse(
            $"{timetable.SameAs ?? stationId}:{timeText}:{timetableIndex}:{itemIndex}",
            "metro",
            stationId,
            stopName,
            item.Train?.Trim(),
            item.TrainNumber?.Trim(),
            timetable.Railway?.Trim(),
            lineName,
            destinations.Count > 0 ? string.Join("／", destinations) : null,
            null,
            scheduledAt,
            null,
            timetable.UpdatedAt,
            "表定時刻",
            item.IsLast,
            platform);
    }

    private static HashSet<string> SelectApplicableCalendars(
        IReadOnlyList<OdptStationTimetable> timetables,
        IReadOnlyList<OdptCalendar> calendars,
        DateOnly date,
        DayOfWeek dayOfWeek)
    {
        var available = timetables
            .Select(item => item.Calendar?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (available.Count == 0)
        {
            return [];
        }

        var dateMatches = calendars
            .Where(item => IsDateIncluded(item, date))
            .Select(item => item.SameAs?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item) && available.Contains(item))
            .Cast<string>()
            .ToList();
        var specificMatches = dateMatches
            .Where(item => item.Contains(".Specific.", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (specificMatches.Count > 0)
        {
            return specificMatches;
        }

        var holidayMatches = dateMatches
            .Where(item => CalendarKey(item) is "holiday" or "saturdayholiday")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (holidayMatches.Count > 0)
        {
            return holidayMatches;
        }

        var candidates = dayOfWeek switch
        {
            DayOfWeek.Saturday => new[] { "saturdayholiday", "saturday", "holiday" },
            DayOfWeek.Sunday => new[] { "saturdayholiday", "holiday", "sunday" },
            _ => new[] { "weekday", dayOfWeek.ToString().ToLowerInvariant() }
        };
        foreach (var candidate in candidates)
        {
            var matches = available
                .Where(item => CalendarKey(item) == candidate)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (matches.Count > 0)
            {
                return matches;
            }
        }

        return [];
    }

    private static bool IsDateIncluded(OdptCalendar calendar, DateOnly date)
    {
        if (calendar.Days.Contains(date))
        {
            return true;
        }

        var range = calendar.Duration?.Split('/', StringSplitOptions.TrimEntries);
        return range is { Length: 2 } &&
               DateOnly.TryParseExact(
                   range[0],
                   "yyyy-MM-dd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out var start) &&
               DateOnly.TryParseExact(
                   range[1],
                   "yyyy-MM-dd",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out var end) &&
               date >= start &&
               date <= end;
    }

    private static string CalendarKey(string calendarId) =>
        calendarId.Split(':', '.').LastOrDefault()?.ToLowerInvariant() ?? string.Empty;

    private static string GetStationName(
        string stationId,
        IReadOnlyDictionary<string, string> stationNames) =>
        stationNames.TryGetValue(stationId, out var name)
            ? name
            : stationId.Split(':', '.').LastOrDefault() ?? stationId;

    private static string? GetRailwayName(string? railwayId)
    {
        var key = railwayId?.Split('.').LastOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return RailwayNames.TryGetValue(key, out var name) ? name : key;
    }
}
