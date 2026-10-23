using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Hko;

public static class HkoAlertMapper
{
    private const string WarningSourceUrl =
        "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=warningInfo&lang=tc";
    private const string TipsSourceUrl =
        "https://data.weather.gov.hk/weatherAPI/opendata/weather.php?dataType=swt&lang=tc";
    private const string EarthquakeSourceUrl =
        "https://data.weather.gov.hk/weatherAPI/opendata/earthquake.php?dataType=feltearthquake&lang=tc";
    private static readonly TimeSpan RecentEarthquakeWindow = TimeSpan.FromHours(24);

    public static IReadOnlyList<LocalAlertResponse> Map(HkoAlertBundle bundle)
    {
        using var summaryDocument = JsonDocument.Parse(bundle.WarningSummaryJson);
        using var infoDocument = JsonDocument.Parse(bundle.WarningInfoJson);
        using var tipsDocument = JsonDocument.Parse(bundle.SpecialWeatherTipsJson);
        using var earthquakeDocument = JsonDocument.Parse(bundle.FeltEarthquakeJson);

        var details = ReadWarningDetails(infoDocument.RootElement);
        var alerts = MapWarnings(summaryDocument.RootElement, details).ToList();
        alerts.AddRange(MapTips(tipsDocument.RootElement));

        var earthquake = MapFeltEarthquake(earthquakeDocument.RootElement, bundle.FetchedAt);
        if (earthquake is not null)
        {
            alerts.Add(earthquake);
        }

        return alerts
            .Where(alert => !alert.ExpiresAt.HasValue || alert.ExpiresAt.Value >= bundle.FetchedAt)
            .OrderByDescending(alert => SeverityRank(alert.Severity))
            .ThenByDescending(alert => alert.UpdatedAt)
            .ToArray();
    }

    private static IEnumerable<LocalAlertResponse> MapWarnings(
        JsonElement root,
        IReadOnlyDictionary<string, WarningDetail> details)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var value = property.Value;
            var actionCode = String(value, "actionCode");
            if (string.Equals(actionCode, "CANCEL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = String(value, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var code = String(value, "code") ?? property.Name;
            var type = String(value, "type");
            details.TryGetValue(property.Name, out var detail);
            detail ??= details.Values.FirstOrDefault(item =>
                string.Equals(item.Subtype, code, StringComparison.OrdinalIgnoreCase));

            var title = BuildWarningTitle(name, type);
            var summary = detail is null
                ? title
                : JoinSummary(detail.Contents, title);
            var severity = WarningSeverity(code, property.Name, title);
            var updatedAt = ParseDate(String(value, "updateTime")) ?? detail?.UpdatedAt;
            var effectiveAt = ParseDate(String(value, "issueTime")) ?? updatedAt;
            var expiresAt = ParseDate(String(value, "expireTime"));

            yield return new LocalAlertResponse(
                $"hko-warning-{StableHash($"{property.Name}|{code}|{title}")}",
                WarningKind(property.Name),
                name,
                severity,
                SeverityLabel(severity, type),
                title,
                summary,
                "香港",
                "香港天文台 HKO",
                updatedAt,
                effectiveAt,
                expiresAt,
                WarningSourceUrl);
        }
    }

    private static IEnumerable<LocalAlertResponse> MapTips(JsonElement root)
    {
        if (!root.TryGetProperty("swt", out var tips) || tips.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in tips.EnumerateArray())
        {
            var description = String(item, "desc")?.Trim();
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var updatedAt = ParseDate(String(item, "updateTime"));
            var severity = TipSeverity(description);
            yield return new LocalAlertResponse(
                $"hko-tip-{StableHash($"{updatedAt:O}|{description}")}",
                "weather",
                "特別天氣提示",
                severity,
                SeverityLabel(severity, null),
                FirstSentence(description, 72),
                description,
                "香港",
                "香港天文台 HKO",
                updatedAt,
                updatedAt,
                null,
                TipsSourceUrl);
        }
    }

    private static LocalAlertResponse? MapFeltEarthquake(
        JsonElement root,
        DateTimeOffset fetchedAt)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var occurredAt = ParseDate(String(root, "ptime"));
        var updatedAt = ParseDate(String(root, "updateTime")) ?? occurredAt;
        if (!occurredAt.HasValue ||
            occurredAt.Value < fetchedAt.Subtract(RecentEarthquakeWindow) ||
            occurredAt.Value > fetchedAt.AddMinutes(5))
        {
            return null;
        }

        var region = String(root, "region") ?? "香港附近";
        var magnitude = String(root, "mag");
        var intensity = String(root, "intensity");
        var details = String(root, "details") ?? $"香港錄得來自{region}的有感地震。";
        var title = string.IsNullOrWhiteSpace(magnitude)
            ? $"香港有感地震：{region}"
            : $"香港有感地震：{region}規模 {magnitude}";
        var severity = EarthquakeSeverity(intensity);

        return new LocalAlertResponse(
            $"hko-earthquake-{StableHash($"{occurredAt:O}|{region}|{magnitude}")}",
            "naturalHazard",
            "本地有感地震",
            severity,
            SeverityLabel(severity, intensity),
            title,
            details,
            "香港",
            "香港天文台 HKO",
            updatedAt,
            occurredAt,
            occurredAt.Value.Add(RecentEarthquakeWindow),
            EarthquakeSourceUrl);
    }

    private static Dictionary<string, WarningDetail> ReadWarningDetails(JsonElement root)
    {
        var result = new Dictionary<string, WarningDetail>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("details", out var details) ||
            details.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in details.EnumerateArray())
        {
            var code = String(item, "warningStatementCode");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var contents = item.TryGetProperty("contents", out var values) &&
                           values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()?.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Cast<string>()
                    .ToArray()
                : [];
            result[code] = new WarningDetail(
                contents,
                String(item, "subtype"),
                ParseDate(String(item, "updateTime")));
        }

        return result;
    }

    private static string BuildWarningTitle(string name, string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || name.Contains(type, StringComparison.Ordinal))
        {
            return name;
        }

        return name is "暴雨警告信號" or "火災危險警告"
            ? $"{type}{name}"
            : $"{type}（{name}）";
    }

    private static string WarningKind(string familyCode) =>
        familyCode is "WL" or "WTMW" or "WFNTSA"
            ? "naturalHazard"
            : "weather";

    private static string WarningSeverity(string code, string familyCode, string text)
    {
        if (code is "WRAINB" or "TC10" || text.Contains("黑色", StringComparison.Ordinal) ||
            text.Contains("十號", StringComparison.Ordinal))
        {
            return "emergency";
        }

        if (code is "WRAINR" or "TC8NE" or "TC8SE" or "TC8SW" or "TC8NW" or
            "TC9" or "WFIRER" || familyCode is "WTMW" or "WL" or "WTCPRE8")
        {
            return "severe";
        }

        return "warning";
    }

    private static string TipSeverity(string text)
    {
        if (text.Contains("黑色", StringComparison.Ordinal) ||
            text.Contains("十號", StringComparison.Ordinal))
        {
            return "emergency";
        }

        if (text.Contains("八號", StringComparison.Ordinal) ||
            text.Contains("九號", StringComparison.Ordinal) ||
            text.Contains("海嘯", StringComparison.Ordinal) ||
            text.Contains("嚴重水浸", StringComparison.Ordinal))
        {
            return "severe";
        }

        return "advisory";
    }

    private static string EarthquakeSeverity(string? intensity)
    {
        if (string.IsNullOrWhiteSpace(intensity))
        {
            return "advisory";
        }

        if (intensity.Contains('5') || intensity.Contains('6') || intensity.Contains('7') ||
            intensity.Contains('五') || intensity.Contains('六') || intensity.Contains('七') ||
            intensity.Contains("VII", StringComparison.OrdinalIgnoreCase) ||
            intensity.Contains("VI", StringComparison.OrdinalIgnoreCase) ||
            intensity.Contains('V') ||
            intensity.Contains("強烈", StringComparison.Ordinal))
        {
            return "severe";
        }

        return intensity.Contains('3') || intensity.Contains('4') ||
               intensity.Contains('三') || intensity.Contains('四') ||
               intensity.Contains("III", StringComparison.OrdinalIgnoreCase) ||
               intensity.Contains("IV", StringComparison.OrdinalIgnoreCase)
            ? "warning"
            : "advisory";
    }

    private static string SeverityLabel(string severity, string? officialType)
    {
        if (!string.IsNullOrWhiteSpace(officialType))
        {
            return officialType;
        }

        return severity switch
        {
            "emergency" => "緊急警報",
            "severe" => "嚴重警報",
            "warning" => "警告生效中",
            _ => "官方提示"
        };
    }

    private static string JoinSummary(IReadOnlyList<string> contents, string fallback)
    {
        if (contents.Count == 0)
        {
            return fallback;
        }

        var value = string.Join(" ", contents.Take(3));
        return value.Length <= 700 ? value : $"{value[..697]}…";
    }

    private static string FirstSentence(string text, int maximumLength)
    {
        var newline = text.IndexOfAny(['\n', '\r']);
        var punctuation = text.IndexOfAny(['。', '！', '？']);
        var end = new[] { newline, punctuation >= 0 ? punctuation + 1 : -1 }
            .Where(index => index > 0)
            .DefaultIfEmpty(text.Length)
            .Min();
        var value = text[..Math.Min(end, text.Length)].Trim();
        return value.Length <= maximumLength
            ? value
            : $"{value[..(maximumLength - 1)]}…";
    }

    private static string? String(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private static int SeverityRank(string severity) => severity switch
    {
        "emergency" => 4,
        "severe" => 3,
        "warning" => 2,
        "advisory" => 1,
        _ => 0
    };

    private static string StableHash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();

    private sealed record WarningDetail(
        IReadOnlyList<string> Contents,
        string? Subtype,
        DateTimeOffset? UpdatedAt);
}
