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
            ["Fukutoshin"] = "副都心線",
            ["Asakusa"] = "浅草線",
            ["Mita"] = "三田線",
            ["Shinjuku"] = "新宿線",
            ["Oedo"] = "大江戸線"
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
        var stationNames = stations
            .Select(item => item.StationTitle!.Ja!.Trim())
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
            [GetOperatorName(railway.Operator)],
            directions,
            stationNames);
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

    public static MetroServiceStatusResponse? MapTrainInformation(
        OdptTrainInformation information,
        DateTimeOffset now)
    {
        var id = information.SameAs?.Trim();
        var lineId = information.Railway?.Trim();
        var messageJa = information.TrainInformationText?.Ja?.Trim();
        var messageEn = information.TrainInformationText?.En?.Trim();
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(lineId) ||
            (string.IsNullOrWhiteSpace(messageJa) && string.IsNullOrWhiteSpace(messageEn)) ||
            !information.ValidUntil.HasValue ||
            information.ValidUntil.Value <= now)
        {
            return null;
        }

        return new MetroServiceStatusResponse(
            id,
            lineId,
            information.RailwayTitle?.Ja?.Trim() ?? GetRailwayName(lineId),
            messageJa,
            messageEn,
            information.UpdatedAt,
            information.ValidUntil,
            StatusSummary(messageJa, messageEn));
    }

    public static IReadOnlyList<TransitArrivalResponse> MapStationDepartures(
        IReadOnlyList<OdptStationTimetable> timetables,
        IReadOnlyList<OdptCalendar> calendars,
        IReadOnlyDictionary<string, string> stationNames,
        DateTimeOffset now,
        IReadOnlyList<OdptRailway>? railways = null,
        IReadOnlyList<OdptTrain>? trains = null)
    {
        var tokyoNow = TimeZoneInfo.ConvertTime(now, TokyoTimeZone);
        var serviceDate = DateOnly.FromDateTime(tokyoNow.DateTime);
        var applicableCalendars = SelectApplicableCalendars(
            timetables,
            calendars,
            serviceDate,
            tokyoNow.DayOfWeek);

        return SelectApplicableTimetables(timetables, applicableCalendars)
            .SelectMany((timetable, timetableIndex) => timetable.Objects.Select(
                (item, itemIndex) => MapDeparture(
                    timetable,
                    item,
                    stationNames,
                    railways ?? [],
                    trains ?? [],
                    now,
                    serviceDate,
                    timetableIndex,
                    itemIndex)))
            .Where(item => item is not null && (item.EstimatedAt ?? item.ScheduledAt) >= now)
            .Cast<TransitArrivalResponse>()
            .OrderBy(item => item.ScheduledAt)
            .Take(20)
            .ToList();
    }

    public static DateTimeOffset? GetEndedServiceDayLastDeparture(
        IReadOnlyList<OdptStationTimetable> timetables,
        IReadOnlyList<OdptCalendar> calendars,
        DateTimeOffset now)
    {
        var tokyoNow = TimeZoneInfo.ConvertTime(now, TokyoTimeZone);
        var serviceDate = DateOnly.FromDateTime(tokyoNow.DateTime);
        var applicableCalendars = SelectApplicableCalendars(
            timetables,
            calendars,
            serviceDate,
            tokyoNow.DayOfWeek);
        var scheduledDepartures = SelectApplicableTimetables(timetables, applicableCalendars)
            .SelectMany(item => item.Objects)
            .Select(item => ParseScheduledAt(
                item.DepartureTime?.Trim() ?? item.ArrivalTime?.Trim(),
                serviceDate))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();

        if (scheduledDepartures.Count == 0 || scheduledDepartures.Any(item => item >= now))
        {
            return null;
        }

        return scheduledDepartures.Max();
    }

    private static TransitArrivalResponse? MapDeparture(
        OdptStationTimetable timetable,
        OdptStationTimetableObject item,
        IReadOnlyDictionary<string, string> stationNames,
        IReadOnlyList<OdptRailway> railways,
        IReadOnlyList<OdptTrain> trains,
        DateTimeOffset now,
        DateOnly serviceDate,
        int timetableIndex,
        int itemIndex)
    {
        var timeText = item.DepartureTime?.Trim() ?? item.ArrivalTime?.Trim();
        var scheduledAt = ParseScheduledAt(timeText, serviceDate);
        if (!scheduledAt.HasValue)
        {
            return null;
        }
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
        var liveTrain = FindLiveTrain(timetable, item, trains, now);
        var delaySeconds = liveTrain?.Delay is >= 0 ? liveTrain.Delay.Value : (int?)null;
        var estimatedAt = delaySeconds.HasValue
            ? scheduledAt.Value.AddSeconds(delaySeconds.Value)
            : (DateTimeOffset?)null;

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
            ResolveDirection(timetable, item, railways),
            scheduledAt,
            estimatedAt,
            liveTrain?.UpdatedAt ?? timetable.UpdatedAt,
            GetDepartureStatus(delaySeconds),
            item.IsLast,
            platform);
    }

    private static DateTimeOffset? ParseScheduledAt(
        string? timeText,
        DateOnly serviceDate)
    {
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
        return new DateTimeOffset(
            localDateTime,
            TokyoTimeZone.GetUtcOffset(localDateTime));
    }

    private static IEnumerable<OdptStationTimetable> SelectApplicableTimetables(
        IReadOnlyList<OdptStationTimetable> timetables,
        IReadOnlySet<string> applicableCalendars) =>
        timetables.Where(item =>
            applicableCalendars.Count == 0 ||
            string.IsNullOrWhiteSpace(item.Calendar) ||
            applicableCalendars.Contains(item.Calendar));

    private static OdptTrain? FindLiveTrain(
        OdptStationTimetable timetable,
        OdptStationTimetableObject item,
        IReadOnlyList<OdptTrain> trains,
        DateTimeOffset now)
    {
        var trainId = item.Train?.Trim();
        var trainNumber = item.TrainNumber?.Trim();
        if (string.IsNullOrWhiteSpace(trainId) && string.IsNullOrWhiteSpace(trainNumber))
        {
            return null;
        }

        return trains
            .Where(train => train.ValidUntil.HasValue && train.ValidUntil.Value > now)
            .Where(train => string.IsNullOrWhiteSpace(timetable.Railway) ||
                            string.Equals(
                                train.Railway,
                                timetable.Railway,
                                StringComparison.OrdinalIgnoreCase))
            .Where(train =>
                (!string.IsNullOrWhiteSpace(trainId) && string.Equals(
                    train.SameAs,
                    trainId,
                    StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(trainNumber) && string.Equals(
                    train.TrainNumber,
                    trainNumber,
                    StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(train => train.UpdatedAt)
            .FirstOrDefault();
    }

    private static string GetDepartureStatus(int? delaySeconds)
    {
        if (!delaySeconds.HasValue)
        {
            return "表定時刻";
        }
        if (delaySeconds.Value == 0)
        {
            return "即時預估";
        }
        if (delaySeconds.Value < 60)
        {
            return $"延誤 {delaySeconds.Value} 秒";
        }

        var delayMinutes = Math.Ceiling(delaySeconds.Value / 60d);
        return $"延誤 {delayMinutes.ToString(CultureInfo.InvariantCulture)} 分鐘";
    }

    private static int? ResolveDirection(
        OdptStationTimetable timetable,
        OdptStationTimetableObject item,
        IReadOnlyList<OdptRailway> railways)
    {
        var railway = railways.FirstOrDefault(value =>
            value.SameAs?.Equals(timetable.Railway, StringComparison.OrdinalIgnoreCase) == true);
        var orderedStations = railway?.StationOrder
            .OrderBy(value => value.Index)
            .Where(value => !string.IsNullOrWhiteSpace(value.Station))
            .ToList();
        if (orderedStations is not { Count: > 1 })
        {
            return null;
        }

        var first = orderedStations[0].Station!;
        var last = orderedStations[^1].Station!;
        var railDirectionTerminal = timetable.RailDirection?
            .Split(':', 2, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()?
            .Split('.')
            .LastOrDefault();
        if (!string.IsNullOrWhiteSpace(railDirectionTerminal))
        {
            if (last.EndsWith($".{railDirectionTerminal}", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (first.EndsWith($".{railDirectionTerminal}", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
        }

        if (item.DestinationStations.Contains(last, StringComparer.OrdinalIgnoreCase))
        {
            return 0;
        }
        if (item.DestinationStations.Contains(first, StringComparer.OrdinalIgnoreCase))
        {
            return 1;
        }

        var currentIndex = orderedStations.FindIndex(value =>
            value.Station!.Equals(timetable.Station, StringComparison.OrdinalIgnoreCase));
        if (currentIndex >= 0)
        {
            var destinationIndexes = item.DestinationStations
                .Select(destination => orderedStations.FindIndex(value =>
                    value.Station!.Equals(destination, StringComparison.OrdinalIgnoreCase)))
                .Where(index => index >= 0)
                .Distinct()
                .ToList();
            if (destinationIndexes.Count > 0 && destinationIndexes.All(index => index > currentIndex))
            {
                return 0;
            }
            if (destinationIndexes.Count > 0 && destinationIndexes.All(index => index < currentIndex))
            {
                return 1;
            }
        }
        return null;
    }

    private static string StatusSummary(string? messageJa, string? messageEn)
    {
        var combined = $"{messageJa} {messageEn}";
        if (combined.Contains("平常", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("normal", StringComparison.OrdinalIgnoreCase))
        {
            return "目前正常營運。";
        }
        if (combined.Contains("遅延", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("delay", StringComparison.OrdinalIgnoreCase))
        {
            return "目前有班次延誤，請預留候車時間。";
        }
        if (combined.Contains("運転見合わせ", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("suspend", StringComparison.OrdinalIgnoreCase))
        {
            return "目前有路段暫停營運，請參考官方公告。";
        }
        return "目前有官方營運公告，詳細內容請參考原文。";
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

    private static string GetOperatorName(string? operatorId) => operatorId switch
    {
        "odpt.Operator:TokyoMetro" => "Tokyo Metro",
        "odpt.Operator:Toei" => "都營地下鐵",
        _ => operatorId?.Split(':', '.').LastOrDefault() ?? "ODPT"
    };
}
