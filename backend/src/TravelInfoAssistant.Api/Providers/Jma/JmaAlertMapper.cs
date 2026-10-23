using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Jma;

public static class JmaAlertMapper
{
    private static readonly TimeSpan RecentEventWindow = TimeSpan.FromHours(24);
    private static readonly string[] TokyoMarkers =
    [
        "東京都",
        "東京地方",
        "東京湾内湾",
        "伊豆諸島",
        "小笠原諸島",
        "伊豆大島",
        "新島",
        "神津島",
        "三宅島",
        "八丈島",
        "青ヶ島",
        "西之島",
        "硫黄島",
        "福徳岡ノ場",
        "ベヨネース列岩",
        "須美寿島",
        "伊豆鳥島",
        "孀婦岩",
        "海形海山",
        "海徳海山",
        "噴火浅根",
        "北福徳堆",
        "南日吉海山",
        "日光海山"
    ];

    public static IReadOnlyList<LocalAlertResponse> Map(
        JmaDocumentBundle bundle,
        string regionName)
    {
        var reports = bundle.Documents
            .Select(TryParse)
            .Where(report => report is not null)
            .Cast<ParsedReport>()
            .Where(IsTokyoRelevant)
            .ToArray();

        var alerts = new List<LocalAlertResponse>();
        alerts.AddRange(MapCurrentReports(
            reports,
            report => IsWeather(report.InfoKind),
            "weather",
            "氣象警報・注意報",
            regionName));
        alerts.AddRange(MapEarthquakes(reports, regionName, bundle.FetchedAt));
        alerts.AddRange(MapCurrentReports(
            reports,
            report => report.InfoKind.Contains("津波", StringComparison.Ordinal),
            "naturalHazard",
            "海嘯警報・注意報",
            regionName));
        alerts.AddRange(MapVolcanoReports(reports, regionName, bundle.FetchedAt));

        return alerts
            .Where(alert => !alert.ExpiresAt.HasValue || alert.ExpiresAt.Value >= bundle.FetchedAt)
            .OrderByDescending(alert => SeverityRank(alert.Severity))
            .ThenByDescending(alert => alert.UpdatedAt)
            .ToArray();
    }

    private static IEnumerable<LocalAlertResponse> MapCurrentReports(
        IReadOnlyList<ParsedReport> reports,
        Func<ParsedReport, bool> predicate,
        string kind,
        string category,
        string regionName)
    {
        var report = reports
            .Where(predicate)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefault();

        if (report is null || IsCancelled(report))
        {
            return [];
        }

        var activeKinds = ExtractActiveKinds(report.Document);
        if (activeKinds.Count == 0)
        {
            return [];
        }

        return
        [
            ToAlert(report, kind, category, regionName, activeKinds, null)
        ];
    }

    private static IEnumerable<LocalAlertResponse> MapEarthquakes(
        IReadOnlyList<ParsedReport> reports,
        string regionName,
        DateTimeOffset now)
    {
        return LatestPerEvent(reports.Where(report =>
                report.InfoKind.Contains("震源・震度", StringComparison.Ordinal)))
            .Where(report =>
                !IsCancelled(report) &&
                report.UpdatedAt >= now.Subtract(RecentEventWindow))
            .Select(report =>
            {
                var intensity = ExtractTokyoIntensity(report.Document);
                if (string.IsNullOrWhiteSpace(intensity) || IntensityRank(intensity) < 3)
                {
                    return null;
                }

                var title = $"東京都觀測到震度{TranslateIntensity(intensity)}";
                return ToAlert(
                    report,
                    "naturalHazard",
                    "地震資訊",
                    regionName,
                    [title],
                    report.UpdatedAt.Add(RecentEventWindow));
            })
            .Where(alert => alert is not null)
            .Cast<LocalAlertResponse>();
    }

    private static IEnumerable<LocalAlertResponse> MapVolcanoReports(
        IReadOnlyList<ParsedReport> reports,
        string regionName,
        DateTimeOffset now)
    {
        return LatestPerEvent(reports.Where(report =>
                report.InfoKind.Contains("噴火", StringComparison.Ordinal) ||
                report.InfoKind.Contains("火山", StringComparison.Ordinal)))
            .Where(report => !IsCancelled(report))
            .Select(report =>
            {
                var activeKinds = ExtractActiveKinds(report.Document);
                var isStatusUpdate = report.InfoKind.Contains("解説情報", StringComparison.Ordinal);
                if (activeKinds.Count == 0 && !isStatusUpdate)
                {
                    return null;
                }

                IReadOnlyList<string> titleParts = activeKinds.Count > 0
                    ? activeKinds
                    : ["火山活動資訊"];
                var expiresAt = isStatusUpdate
                    ? report.UpdatedAt.Add(RecentEventWindow)
                    : (DateTimeOffset?)null;
                return ToAlert(
                    report,
                    "naturalHazard",
                    "火山警報・資訊",
                    regionName,
                    titleParts,
                    expiresAt);
            })
            .Where(alert => alert is not null)
            .Cast<LocalAlertResponse>();
    }

    private static LocalAlertResponse ToAlert(
        ParsedReport report,
        string kind,
        string category,
        string regionName,
        IReadOnlyList<string> titleParts,
        DateTimeOffset? fallbackExpiresAt)
    {
        var translatedParts = titleParts
            .Select(TranslateAlertName)
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        var title = string.Join("、", translatedParts);
        var summary = string.IsNullOrWhiteSpace(report.Headline)
            ? title
            : report.Headline;
        var severity = DetermineSeverity(string.Join(" ", titleParts));
        var expiresAt = report.ExpiresAt ?? fallbackExpiresAt;
        var idSource = $"{report.InfoKind}|{report.EventId}|{title}|{report.SourceUrl}";

        return new LocalAlertResponse(
            $"jma-{StableHash(idSource)}",
            kind,
            category,
            severity,
            SeverityLabel(severity),
            title,
            summary,
            regionName,
            string.IsNullOrWhiteSpace(report.PublishingOffice)
                ? "日本氣象廳 JMA"
                : $"日本氣象廳 JMA・{report.PublishingOffice}",
            report.UpdatedAt,
            report.EffectiveAt,
            expiresAt,
            report.SourceUrl);
    }

    private static IEnumerable<ParsedReport> LatestPerEvent(IEnumerable<ParsedReport> reports) =>
        reports
            .GroupBy(
                report => string.IsNullOrWhiteSpace(report.EventId)
                    ? report.SourceUrl
                    : $"{report.InfoKind}:{report.EventId}",
                StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(report => report.UpdatedAt).First());

    private static ParsedReport? TryParse(JmaBulletinDocument bulletin)
    {
        try
        {
            var document = XDocument.Parse(bulletin.Xml, LoadOptions.None);
            var infoKind = Value(document, "InfoKind") ?? Value(document, "Title") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(infoKind))
            {
                return null;
            }

            return new ParsedReport(
                document,
                infoKind,
                Value(document, "InfoType") ?? string.Empty,
                Value(document, "EventID") ?? string.Empty,
                Value(document, "PublishingOffice") ?? string.Empty,
                Value(document, "Text") ?? string.Empty,
                ParseDate(Value(document, "ReportDateTime")) ?? bulletin.UpdatedAt,
                ParseDate(Value(document, "TargetDateTime")),
                ParseDate(Value(document, "ValidDateTime")),
                bulletin.SourceUrl);
        }
        catch (Exception exception) when (
            exception is System.Xml.XmlException or InvalidOperationException)
        {
            return null;
        }
    }

    private static bool IsTokyoRelevant(ParsedReport report)
    {
        var values = report.Document
            .Descendants()
            .Where(element => element.Name.LocalName is "Name" or "Title" or "Text")
            .Select(element => element.Value);
        return values.Any(value => TokyoMarkers.Any(marker =>
            value.Contains(marker, StringComparison.Ordinal)));
    }

    private static IReadOnlyList<string> ExtractActiveKinds(XDocument document)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Item")
            .Where(ItemIsTokyoRelevant)
            .SelectMany(item => item.Descendants()
                .Where(element => element.Name.LocalName == "Kind"))
            .Where(kind => !IsInactiveKind(kind))
            .Select(kind => kind.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "Name")?.Value.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Where(name =>
                !name.Contains("なし", StringComparison.Ordinal) &&
                !name.Contains("解除", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool ItemIsTokyoRelevant(XElement item)
    {
        var areaNames = item.Descendants()
            .Where(element => element.Name.LocalName == "Area")
            .SelectMany(area => area.DescendantsAndSelf()
                .Where(element => element.Name.LocalName == "Name"))
            .Select(element => element.Value);
        return areaNames.Any(value => TokyoMarkers.Any(marker =>
            value.Contains(marker, StringComparison.Ordinal)));
    }

    private static bool IsInactiveKind(XElement kind)
    {
        var status = kind.Elements()
            .FirstOrDefault(element => element.Name.LocalName is "Status" or "Condition")
            ?.Value;
        return status is not null &&
               (status.Contains("解除", StringComparison.Ordinal) ||
                status.Contains("解消", StringComparison.Ordinal) ||
                status.Contains("取消", StringComparison.Ordinal));
    }

    private static string? ExtractTokyoIntensity(XDocument document)
    {
        var prefecture = document.Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "Pref" &&
                element.Elements().Any(child =>
                    child.Name.LocalName == "Name" && child.Value == "東京都"));
        return prefecture?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "MaxInt")?.Value.Trim();
    }

    private static bool IsCancelled(ParsedReport report) =>
        report.InfoType.Contains("取消", StringComparison.Ordinal);

    private static bool IsWeather(string infoKind) =>
        infoKind.Contains("気象", StringComparison.Ordinal) &&
        (infoKind.Contains("警報", StringComparison.Ordinal) ||
         infoKind.Contains("注意報", StringComparison.Ordinal));

    private static string DetermineSeverity(string source)
    {
        if (ContainsAny(source, "特別警報", "大津波警報", "震度７", "震度7", "震度６強", "震度6強"))
        {
            return "emergency";
        }

        if (ContainsAny(source, "警報", "震度６弱", "震度6弱", "震度５強", "震度5強", "震度５弱", "震度5弱"))
        {
            return "severe";
        }

        if (ContainsAny(source, "注意報", "噴火速報", "震度４", "震度4"))
        {
            return "warning";
        }

        if (ContainsAny(source, "震度３", "震度3"))
        {
            return "advisory";
        }

        return "info";
    }

    private static string SeverityLabel(string severity) => severity switch
    {
        "emergency" => "特別警報",
        "severe" => "警報",
        "warning" => "注意報",
        "advisory" => "注意資訊",
        _ => "官方資訊"
    };

    private static string TranslateAlertName(string value)
    {
        return value
            .Replace("大雨", "豪雨", StringComparison.Ordinal)
            .Replace("波浪", "巨浪", StringComparison.Ordinal)
            .Replace("高潮", "風暴潮", StringComparison.Ordinal)
            .Replace("雷", "雷雨", StringComparison.Ordinal)
            .Replace("低温", "低溫", StringComparison.Ordinal)
            .Replace("なだれ", "雪崩", StringComparison.Ordinal)
            .Replace("着雪", "積雪", StringComparison.Ordinal)
            .Replace("津波", "海嘯", StringComparison.Ordinal);
    }

    private static string TranslateIntensity(string intensity) => intensity switch
    {
        "5-" => "5弱",
        "5+" => "5強",
        "6-" => "6弱",
        "6+" => "6強",
        _ => intensity
    };

    private static int IntensityRank(string intensity) => intensity switch
    {
        "7" => 9,
        "6+" => 8,
        "6-" => 7,
        "5+" => 6,
        "5-" => 5,
        "4" => 4,
        "3" => 3,
        "2" => 2,
        "1" => 1,
        _ => 0
    };

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(candidate, StringComparison.Ordinal));

    private static int SeverityRank(string severity) => severity switch
    {
        "emergency" => 5,
        "severe" => 4,
        "warning" => 3,
        "advisory" => 2,
        "info" => 1,
        _ => 0
    };

    private static string StableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    private static string? Value(XDocument document, string localName) =>
        document.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value.Trim();

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

    private sealed record ParsedReport(
        XDocument Document,
        string InfoKind,
        string InfoType,
        string EventId,
        string PublishingOffice,
        string Headline,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? EffectiveAt,
        DateTimeOffset? ExpiresAt,
        string SourceUrl);
}
