using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public static partial class LtaGtfsStaticParser
{
    public static LtaGtfsNetwork ParseNetwork(
        byte[] scheduleZip,
        byte[] chineseNamesZip)
    {
        var routes = ReadCsv(scheduleZip, "routes.txt")
            .Select(row => new LtaGtfsRoute(
                Get(row, "route_id"),
                Get(row, "route_short_name"),
                Get(row, "route_long_name"),
                GetOptional(row, "agency_id")))
            .Where(route => !string.IsNullOrWhiteSpace(route.Id))
            .ToList();
        var stops = ReadCsv(scheduleZip, "stops.txt")
            .Select(row => new LtaGtfsStop(
                Get(row, "stop_id"),
                Get(row, "stop_code"),
                Get(row, "stop_name"),
                GetOptional(row, "parent_station"),
                ParseInt(GetOptional(row, "location_type")) ?? 0,
                GetOptional(row, "platform_code"),
                ParseDouble(GetOptional(row, "stop_lat")),
                ParseDouble(GetOptional(row, "stop_lon"))))
            .Where(stop => !string.IsNullOrWhiteSpace(stop.Id))
            .ToList();
        var trips = ReadCsv(scheduleZip, "trips.txt")
            .Select(row => new LtaGtfsTrip(
                Get(row, "trip_id"),
                Get(row, "route_id"),
                GetOptional(row, "trip_headsign"),
                ParseInt(GetOptional(row, "direction_id")) ?? 0,
                GetOptional(row, "service_id")))
            .Where(trip =>
                !string.IsNullOrWhiteSpace(trip.Id) &&
                !string.IsNullOrWhiteSpace(trip.RouteId))
            .ToList();
        var stopTimes = ReadCsv(scheduleZip, "stop_times.txt")
            .Select(row => new LtaGtfsStopTime(
                Get(row, "trip_id"),
                Get(row, "stop_id"),
                ParseInt(GetOptional(row, "stop_sequence")) ?? 0,
                GetOptional(row, "arrival_time"),
                GetOptional(row, "departure_time")))
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.TripId) &&
                !string.IsNullOrWhiteSpace(item.StopId))
            .ToList();
        var calendars = ReadCsv(scheduleZip, "calendar.txt", required: false)
            .Select(row => new LtaGtfsCalendar(
                Get(row, "service_id"),
                ParseDate(GetOptional(row, "start_date")) ?? DateOnly.MinValue,
                ParseDate(GetOptional(row, "end_date")) ?? DateOnly.MaxValue,
                CalendarDays(row)))
            .Where(item => !string.IsNullOrWhiteSpace(item.ServiceId))
            .ToList();
        var calendarDates = ReadCsv(scheduleZip, "calendar_dates.txt", required: false)
            .Select(row => new LtaGtfsCalendarDate(
                Get(row, "service_id"),
                ParseDate(GetOptional(row, "date")) ?? DateOnly.MinValue,
                ParseInt(GetOptional(row, "exception_type")) ?? 0))
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.ServiceId) &&
                item.Date != DateOnly.MinValue &&
                item.ExceptionType is 1 or 2)
            .ToList();

        var tripLookup = trips.ToDictionary(trip => trip.Id, StringComparer.OrdinalIgnoreCase);
        var routeStops = stopTimes
            .Where(item => tripLookup.ContainsKey(item.TripId))
            .Select(item =>
            {
                var trip = tripLookup[item.TripId];
                return new LtaGtfsRouteStop(
                    trip.RouteId,
                    item.StopId,
                    trip.DirectionId,
                    item.Sequence);
            })
            .GroupBy(
                item => $"{item.RouteId}\u001f{item.DirectionId}\u001f{item.StopId}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.Sequence).First())
            .ToList();

        return new LtaGtfsNetwork(
            routes,
            stops,
            trips,
            routeStops,
            ParseChineseNames(chineseNamesZip),
            stopTimes,
            calendars,
            calendarDates);
    }

    internal static IReadOnlyList<LtaChineseStationName> ParseChineseNames(byte[] zipBytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var zipStream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(item =>
            item.FullName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return [];
        }

        using var workbook = new MemoryStream();
        using (var entryStream = entry.Open())
        {
            entryStream.CopyTo(workbook);
        }
        workbook.Position = 0;

        using var reader = ExcelReaderFactory.CreateReader(workbook);
        var names = new List<LtaChineseStationName>();
        do
        {
            Dictionary<string, int>? columns = null;
            while (reader.Read())
            {
                var values = Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.GetValue(index)?.ToString()?.Trim() ?? string.Empty)
                    .ToList();
                if (columns is null)
                {
                    columns = TryFindColumns(values);
                    continue;
                }

                var stationCode = Value(values, columns, "stationCode");
                var nameEn = Value(values, columns, "nameEn");
                var nameZh = Value(values, columns, "nameZh");
                if (string.IsNullOrWhiteSpace(stationCode) ||
                    string.IsNullOrWhiteSpace(nameZh))
                {
                    continue;
                }

                foreach (Match match in StationCodePattern().Matches(stationCode))
                {
                    names.Add(new LtaChineseStationName(
                        match.Value.ToUpperInvariant(),
                        nameEn,
                        nameZh,
                        Value(values, columns, "lineEn"),
                        Value(values, columns, "lineZh")));
                }
            }
        }
        while (reader.NextResult());

        return names
            .GroupBy(item => item.StationCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ReadCsv(
        byte[] zipBytes,
        string fileName,
        bool required = true)
    {
        using var zipStream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(item =>
            item.FullName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            if (required)
            {
                throw new InvalidOperationException($"LTA GTFS archive is missing {fileName}.");
            }

            return [];
        }
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var headerLine = reader.ReadLine()
            ?? throw new InvalidOperationException($"LTA GTFS {fileName} is empty.");
        var headers = ParseCsvLine(headerLine)
            .Select(header => header.TrimStart('\uFEFF'))
            .ToList();
        var rows = new List<IReadOnlyDictionary<string, string>>();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index]] = index < values.Count ? values[index] : string.Empty;
            }
            rows.Add(row);
        }

        return rows;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }
        values.Add(current.ToString());
        return values;
    }

    private static Dictionary<string, int>? TryFindColumns(IReadOnlyList<string> values)
    {
        var normalized = values
            .Select((value, index) => (Value: NormalizeHeader(value), Index: index))
            .ToList();
        var stationCode = Find(normalized, "stationcode", "stncode");
        var nameEn = Find(normalized, "mrtstationenglish", "stationenglish", "stationnameenglish");
        var nameZh = Find(normalized, "mrtstationchinese", "stationchinese", "stationnamechinese");
        if (!stationCode.HasValue || !nameEn.HasValue || !nameZh.HasValue)
        {
            return null;
        }

        return new Dictionary<string, int>
        {
            ["stationCode"] = stationCode.Value,
            ["nameEn"] = nameEn.Value,
            ["nameZh"] = nameZh.Value,
            ["lineEn"] = Find(normalized, "mrtlineenglish", "lineenglish") ?? -1,
            ["lineZh"] = Find(normalized, "mrtlinechinese", "linechinese") ?? -1
        };
    }

    private static int? Find(
        IReadOnlyList<(string Value, int Index)> values,
        params string[] candidates) =>
        values.FirstOrDefault(item => candidates.Contains(item.Value)).Index is var index &&
        values.Any(item => item.Index == index && candidates.Contains(item.Value))
            ? index
            : null;

    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string Value(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columns,
        string key) =>
        columns.TryGetValue(key, out var index) && index >= 0 && index < values.Count
            ? values[index]
            : string.Empty;

    private static string Get(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;

    private static string? GetOptional(
        IReadOnlyDictionary<string, string> row,
        string key)
    {
        var value = Get(row, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(
            value,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
            ? result
            : null;

    private static IReadOnlyList<DayOfWeek> CalendarDays(
        IReadOnlyDictionary<string, string> row)
    {
        var values = new (string Key, DayOfWeek Day)[]
        {
            ("monday", DayOfWeek.Monday),
            ("tuesday", DayOfWeek.Tuesday),
            ("wednesday", DayOfWeek.Wednesday),
            ("thursday", DayOfWeek.Thursday),
            ("friday", DayOfWeek.Friday),
            ("saturday", DayOfWeek.Saturday),
            ("sunday", DayOfWeek.Sunday)
        };
        return values
            .Where(item => Get(row, item.Key) == "1")
            .Select(item => item.Day)
            .ToList();
    }

    [GeneratedRegex("[A-Z]{2,3}\\d+[A-Z]?", RegexOptions.IgnoreCase)]
    private static partial Regex StationCodePattern();
}
