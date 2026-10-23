using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Ncdr;

public static partial class NcdrAlertMapper
{
    private static readonly string[] LocalDateTimeFormats =
    [
        "yyyy/M/d tt h:mm:ss",
        "yyyy/M/d tt hh:mm:ss"
    ];

    public static IReadOnlyList<LocalAlertResponse> Map(
        string xml,
        string regionName,
        DateTimeOffset now)
    {
        var document = XDocument.Parse(xml);
        var alerts = document
            .Descendants()
            .Where(element => element.Name.LocalName == "entry")
            .Select(entry => MapEntry(entry, regionName))
            .Where(alert => alert is not null)
            .Cast<LocalAlertResponse>()
            .Where(alert => !alert.ExpiresAt.HasValue || alert.ExpiresAt.Value >= now)
            .DistinctBy(alert => alert.Id, StringComparer.Ordinal)
            .OrderByDescending(alert => alert.UpdatedAt)
            .ToArray();

        return alerts;
    }

    private static LocalAlertResponse? MapEntry(XElement entry, string regionName)
    {
        var status = Value(entry, "status");
        var messageType = Value(entry, "msgType");
        if (!string.Equals(status, "Actual", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(messageType, "Cancel", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rawId = Value(entry, "id");
        var title = NormalizeText(Value(entry, "title"));
        var category = entry.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "category")?
            .Attribute("term")?.Value;
        category = NormalizeText(category);
        var summary = NormalizeText(Value(entry, "summary"));
        var sourceName = NormalizeText(entry.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "name")?.Value);
        var sourceUrl = entry.Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName == "link" &&
                string.Equals(element.Attribute("rel")?.Value, "alternate", StringComparison.OrdinalIgnoreCase))?
            .Attribute("href")?.Value;

        if (string.IsNullOrWhiteSpace(rawId) ||
            string.IsNullOrWhiteSpace(title) ||
            string.IsNullOrWhiteSpace(summary) ||
            string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        var stableId = CreateStableId(rawId, summary);
        return new LocalAlertResponse(
            stableId,
            GetKind(category),
            string.IsNullOrWhiteSpace(category) ? title : category,
            "unknown",
            "官方生效中",
            title,
            summary,
            regionName,
            string.IsNullOrWhiteSpace(sourceName) ? "民生示警公開資料平台" : sourceName,
            ParseOffset(Value(entry, "updated")),
            ParseLocalTime(Value(entry, "effective")),
            ParseLocalTime(Value(entry, "expires")),
            sourceUrl);
    }

    private static string Value(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value ?? string.Empty;

    private static DateTimeOffset? ParseOffset(string value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed
            : null;

    private static DateTimeOffset? ParseLocalTime(string value)
    {
        var normalized = value
            .Replace("上午", "AM", StringComparison.Ordinal)
            .Replace("下午", "PM", StringComparison.Ordinal);
        if (!DateTime.TryParseExact(
                normalized,
                LocalDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return null;
        }

        return new DateTimeOffset(parsed, TimeSpan.FromHours(8));
    }

    private static string NormalizeText(string? value)
    {
        var decoded = WebUtility.HtmlDecode(value ?? string.Empty);
        var withoutTags = HtmlTagRegex().Replace(decoded, " ");
        return WhitespaceRegex().Replace(withoutTags, " ").Trim();
    }

    private static string CreateStableId(string rawId, string summary)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{rawId}|{summary}"));
        return $"{rawId}:{Convert.ToHexString(hash.AsSpan(0, 6)).ToLowerInvariant()}";
    }

    private static string GetKind(string category)
    {
        if (ContainsAny(category, "道路", "鐵路", "捷運", "高速公路", "停駛"))
        {
            return "transport";
        }

        if (ContainsAny(category, "颱風", "降雨", "雷雨", "強風", "高溫", "低溫", "濃霧", "巨浪"))
        {
            return "weather";
        }

        if (ContainsAny(category, "地震", "海嘯", "淹水", "土石流", "崩塌", "河川", "水庫", "火山", "堰塞湖"))
        {
            return "naturalHazard";
        }

        if (ContainsAny(category, "空氣品質", "傳染病", "急門診", "海洋污染", "海灘水質"))
        {
            return "health";
        }

        if (ContainsAny(category, "停水", "電力", "通訊", "水門", "天然氣"))
        {
            return "infrastructure";
        }

        if (ContainsAny(category, "防空", "火災", "疏散", "輻射", "停班停課", "災情"))
        {
            return "publicSafety";
        }

        return "other";
    }

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
