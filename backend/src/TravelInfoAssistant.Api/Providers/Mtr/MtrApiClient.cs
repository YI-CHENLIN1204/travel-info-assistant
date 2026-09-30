using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
}

public sealed class MtrApiClient(
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
}
