using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace TravelInfoAssistant.Api.Providers.Kmb;

public static class KmbGtfsParser
{
    public static KmbGtfsSchedule Parse(byte[] zipBytes)
    {
        var routes = Read(zipBytes, "routes.txt")
            .Select(row => new KmbGtfsRoute(
                Get(row, "route_id"),
                Get(row, "agency_id"),
                Get(row, "route_short_name")))
            .Where(item => item.AgencyId is "KMB" or "LWB")
            .ToList();
        var routeIds = routes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trips = Read(zipBytes, "trips.txt")
            .Select(row => new KmbGtfsTrip(
                Get(row, "trip_id"),
                Get(row, "route_id"),
                Get(row, "service_id"),
                ParseInt(Get(row, "direction_id")) ?? 0))
            .Where(item => routeIds.Contains(item.RouteId))
            .ToList();
        var tripIds = trips.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var frequencies = Read(zipBytes, "frequencies.txt", false)
            .Select(row => new KmbGtfsFrequency(
                Get(row, "trip_id"),
                Get(row, "start_time"),
                Get(row, "end_time"),
                ParseInt(Get(row, "headway_secs")) ?? 0))
            .Where(item => tripIds.Contains(item.TripId))
            .ToList();
        var stopTimes = Read(zipBytes, "stop_times.txt", false)
            .Select(row => new KmbGtfsStopTime(
                Get(row, "trip_id"),
                ParseInt(Get(row, "stop_sequence")) ?? int.MaxValue,
                Optional(row, "departure_time")))
            .Where(item => tripIds.Contains(item.TripId))
            .ToList();
        var calendars = Read(zipBytes, "calendar.txt", false)
            .Select(row => new KmbGtfsCalendar(
                Get(row, "service_id"),
                ParseDate(Get(row, "start_date")) ?? DateOnly.MinValue,
                ParseDate(Get(row, "end_date")) ?? DateOnly.MaxValue,
                Days(row)))
            .ToList();
        var calendarDates = Read(zipBytes, "calendar_dates.txt", false)
            .Select(row => new KmbGtfsCalendarDate(
                Get(row, "service_id"),
                ParseDate(Get(row, "date")) ?? DateOnly.MinValue,
                ParseInt(Get(row, "exception_type")) ?? 0))
            .Where(item => item.Date != DateOnly.MinValue && item.ExceptionType is 1 or 2)
            .ToList();

        return new KmbGtfsSchedule(routes, trips, frequencies, stopTimes, calendars, calendarDates);
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
            if (required) throw new InvalidOperationException($"KMB GTFS archive is missing {fileName}.");
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
