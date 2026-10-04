using System.Globalization;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Mtr;

public sealed record MtrHttpResult<T>(
    T Data,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceUpdatedAt = null);

public interface IMtrApiClient
{
    Task<MtrHttpResult<IReadOnlyList<MtrStationRow>>> GetLinesAndStationsAsync(
        CancellationToken cancellationToken);

    Task<MtrHttpResult<MtrScheduleResponse>> GetScheduleAsync(
        string lineCode,
        string stationCode,
        CancellationToken cancellationToken);

    Task<MtrHttpResult<IReadOnlyList<MtrLastTrainSchedule>>> GetLastTrainSchedulesAsync(
        string stationId,
        CancellationToken cancellationToken);
}

public sealed partial class MtrApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MtrOptions> options,
    TimeProvider timeProvider) : IMtrApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MtrHttpResult<IReadOnlyList<MtrStationRow>>> GetLinesAndStationsAsync(
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("mtr-static");
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri($"./{options.Value.LinesAndStationsPath.TrimStart('/')}", UriKind.Relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var csv = await response.Content.ReadAsStringAsync(cancellationToken);
        return new MtrHttpResult<IReadOnlyList<MtrStationRow>>(
            ParseLinesAndStations(csv),
            timeProvider.GetUtcNow(),
            response.Content.Headers.LastModified);
    }

    public async Task<MtrHttpResult<MtrScheduleResponse>> GetScheduleAsync(
        string lineCode,
        string stationCode,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["line"] = lineCode,
            ["sta"] = stationCode,
            ["lang"] = "TC"
        };
        var requestUri = new Uri(
            $"./getSchedule.php?{string.Join('&', query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"))}",
            UriKind.Relative);

        var client = httpClientFactory.CreateClient("mtr-realtime");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<MtrScheduleResponse>(bytes, JsonOptions)
            ?? throw new InvalidOperationException("MTR returned an empty schedule response.");
        return new MtrHttpResult<MtrScheduleResponse>(
            data,
            timeProvider.GetUtcNow(),
            ParseHongKongTime(data.SystemTime));
    }

    public async Task<MtrHttpResult<IReadOnlyList<MtrLastTrainSchedule>>> GetLastTrainSchedulesAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["mobile-app"] = "true",
            ["query_type"] = "search",
            ["station"] = stationId,
            ["theme"] = "false"
        };
        var requestUri = new Uri(
            $"./{options.Value.ServiceHoursPath.TrimStart('/')}?{string.Join('&', query.Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"))}",
            UriKind.Relative);

        var client = httpClientFactory.CreateClient("mtr-service-hours");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return new MtrHttpResult<IReadOnlyList<MtrLastTrainSchedule>>(
            ParseLastTrainSchedules(html),
            timeProvider.GetUtcNow(),
            response.Content.Headers.LastModified);
    }

    internal static IReadOnlyList<MtrStationRow> ParseLinesAndStations(string csv)
    {
        var rows = new List<MtrStationRow>();
        using var reader = new StringReader(csv.TrimStart('\uFEFF'));
        _ = reader.ReadLine();

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = ParseCsvLine(line);
            if (columns.Count < 7 ||
                string.IsNullOrWhiteSpace(columns[0]) ||
                !decimal.TryParse(
                    columns[6],
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var sequence))
            {
                continue;
            }

            rows.Add(new MtrStationRow(
                columns[0].Trim(),
                columns[1].Trim(),
                columns[2].Trim(),
                columns[3].Trim(),
                columns[4].Trim(),
                columns[5].Trim(),
                decimal.ToInt32(sequence)));
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

    internal static DateTimeOffset? ParseHongKongTime(string? value)
    {
        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong");
        return new DateTimeOffset(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified),
            timeZone.GetUtcOffset(local));
    }

    internal static IReadOnlyList<MtrLastTrainSchedule> ParseLastTrainSchedules(string html)
    {
        var content = HtmlCommentRegex().Replace(html, string.Empty);
        var headers = TrainLineHeaderRegex().Matches(content);
        var schedules = new List<MtrLastTrainSchedule>();

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            var sectionStart = header.Index + header.Length;
            var sectionEnd = index + 1 < headers.Count
                ? headers[index + 1].Index
                : content.Length;
            var section = content[sectionStart..sectionEnd];

            foreach (Match row in TableRowRegex().Matches(section))
            {
                var cells = TableCellRegex().Matches(row.Groups["content"].Value);
                if (cells.Count < 3)
                {
                    continue;
                }

                var destination = DestinationStationIdRegex().Match(cells[0].Value);
                var departureTime = WebUtility.HtmlDecode(
                    HtmlTagRegex().Replace(cells[^1].Groups["content"].Value, string.Empty)).Trim();
                if (!destination.Success || !LastTrainTimeRegex().IsMatch(departureTime))
                {
                    continue;
                }

                schedules.Add(new MtrLastTrainSchedule(
                    header.Groups["line"].Value.ToUpperInvariant(),
                    destination.Groups["id"].Value,
                    departureTime));
            }
        }

        return schedules;
    }

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    [GeneratedRegex(
        "<h2\\b[^>]*class\\s*=\\s*[\\\"'][^\\\"']*\\btrainLine\\s+(?<line>[A-Za-z0-9-]+)[^\\\"']*[\\\"'][^>]*>.*?</h2>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TrainLineHeaderRegex();

    [GeneratedRegex("<tr\\b[^>]*>(?<content>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex("<td\\b[^>]*>(?<content>.*?)</td>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TableCellRegex();

    [GeneratedRegex("\\bjs_station_(?<id>\\d+)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex DestinationStationIdRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("^(?:[01]\\d|2[0-3])[0-5]\\d$")]
    private static partial Regex LastTrainTimeRegex();
}
