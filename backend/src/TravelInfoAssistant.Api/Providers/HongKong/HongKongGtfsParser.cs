using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TravelInfoAssistant.Api.Providers.HongKong;

public static class HongKongGtfsParser
{
    private static readonly HashSet<string> SupportedAgencies =
        new(["KMB", "LWB", "CTB", "NLB"], StringComparer.OrdinalIgnoreCase);

    public static HongKongGtfsSchedule Parse(byte[] zipBytes)
    {
        var routes = Read(zipBytes, "routes.txt")
            .Select(row => new HongKongGtfsRoute(
                Get(row, "route_id"),
                Get(row, "agency_id"),
                Get(row, "route_short_name")))
            .Where(item => SupportedAgencies.Contains(item.AgencyId))
            .ToList();
        var routeIds = routes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trips = Read(zipBytes, "trips.txt")
            .Select(row => new HongKongGtfsTrip(
                Get(row, "trip_id"),
                Get(row, "route_id"),
                Get(row, "service_id"),
                ParseInt(Get(row, "direction_id")) ?? 0))
            .Where(item => routeIds.Contains(item.RouteId))
            .ToList();
        var tripIds = trips.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var frequencies = Read(zipBytes, "frequencies.txt", false)
            .Select(row => new HongKongGtfsFrequency(
                Get(row, "trip_id"),
                Get(row, "start_time"),
                Get(row, "end_time"),
                ParseInt(Get(row, "headway_secs")) ?? 0))
            .Where(item => tripIds.Contains(item.TripId))
            .ToList();
        var stopTimes = Read(zipBytes, "stop_times.txt", false)
            .Select(row => new HongKongGtfsStopTime(
                Get(row, "trip_id"),
                ParseInt(Get(row, "stop_sequence")) ?? int.MaxValue,
                Optional(row, "departure_time"),
                Optional(row, "stop_id")))
            .Where(item => tripIds.Contains(item.TripId))
            .ToList();
        var stopIds = stopTimes
            .Where(item => !string.IsNullOrWhiteSpace(item.StopId))
            .Select(item => item.StopId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stops = Read(zipBytes, "stops.txt", false)
            .Select(row => new HongKongGtfsStop(Get(row, "stop_id"), Get(row, "stop_name")))
            .Where(item => stopIds.Contains(item.Id))
            .ToList();
        var calendars = Read(zipBytes, "calendar.txt", false)
            .Select(row => new HongKongGtfsCalendar(
                Get(row, "service_id"),
                ParseDate(Get(row, "start_date")) ?? DateOnly.MinValue,
                ParseDate(Get(row, "end_date")) ?? DateOnly.MaxValue,
                Days(row)))
            .ToList();
        var calendarDates = Read(zipBytes, "calendar_dates.txt", false)
            .Select(row => new HongKongGtfsCalendarDate(
                Get(row, "service_id"),
                ParseDate(Get(row, "date")) ?? DateOnly.MinValue,
                ParseInt(Get(row, "exception_type")) ?? 0))
            .Where(item => item.Date != DateOnly.MinValue && item.ExceptionType is 1 or 2)
            .ToList();

        return new HongKongGtfsSchedule(
            routes,
            trips,
            frequencies,
            stopTimes,
            calendars,
            calendarDates,
            stops);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> Read(
        byte[] zipBytes,
        string fileName,
        bool required = true)
    {
        using var stream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(item =>
            item.FullName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            if (required) throw new InvalidOperationException($"Hong Kong GTFS archive is missing {fileName}.");
            return [];
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
        var headers = ParseCsv(reader.ReadLine() ?? string.Empty)
            .Select(item => item.TrimStart('\uFEFF'))
            .ToList();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseCsv(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index]] = index < values.Count ? values[index].Trim() : string.Empty;
            }
            rows.Add(row);
        }
        return rows;
    }

    private static IReadOnlyList<string> ParseCsv(string line)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else quoted = !quoted;
            }
            else if (character == ',' && !quoted)
            {
                values.Add(value.ToString());
                value.Clear();
            }
            else value.Append(character);
        }
        values.Add(value.ToString());
        return values;
    }

    private static string Get(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string? Optional(IReadOnlyDictionary<string, string> row, string key)
    {
        var value = Get(row, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static DateOnly? ParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : null;

    private static IReadOnlyList<DayOfWeek> Days(IReadOnlyDictionary<string, string> row) =>
        new (string Key, DayOfWeek Day)[]
        {
            ("monday", DayOfWeek.Monday), ("tuesday", DayOfWeek.Tuesday),
            ("wednesday", DayOfWeek.Wednesday), ("thursday", DayOfWeek.Thursday),
            ("friday", DayOfWeek.Friday), ("saturday", DayOfWeek.Saturday),
            ("sunday", DayOfWeek.Sunday)
        }.Where(item => Get(row, item.Key) == "1").Select(item => item.Day).ToList();
}
