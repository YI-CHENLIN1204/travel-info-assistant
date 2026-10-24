using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Tra;

public static class TraOperationsMapper
{
    public const string SourceUrl = "https://www.railway.gov.tw/tra-tip-web/tip/tip007/tip711/blockList";

    public static IReadOnlyList<LocalAlertResponse> Map(string html)
    {
        var cleanHtml = MatchRegex(@"<!--[\s\S]*?-->").Replace(html, "");
        var blocks = MatchRegex("<dl\\b[^>]*class=[\"'][^\"']*\\bnow_status\\b[^\"']*[\"'][^>]*>(.*?)</dl>\\s*<table\\b[^>]*>(.*?)</table>")
            .Matches(cleanHtml);
        if (blocks.Count == 0) throw new FormatException("TRA operation table not found.");

        var events = new List<(string Line, string Section, string Reason, string Recovery, DateTimeOffset? Time)>();
        foreach (Match block in blocks)
        {
            var lineMatch = MatchRegex(@"<dt\b[^>]*>(.*?)</dt>").Match(block.Groups[1].Value);
            var line = Text(lineMatch.Groups[1].Value);
            var status = Text(block.Groups[1].Value);
            if (string.IsNullOrWhiteSpace(line)) throw new FormatException("TRA line name missing.");
            if (status.Contains("全線正常運行", StringComparison.Ordinal)) continue;

            var rows = MatchRegex(@"<tr\b[^>]*>(.*?)</tr>").Matches(block.Groups[2].Value);
            if (rows.Count == 0) throw new FormatException("TRA disrupted line has no details.");
            foreach (Match row in rows)
            {
                var cells = MatchRegex(@"<td\b[^>]*>(.*?)</td>").Matches(row.Groups[1].Value)
                    .Select(cell => Text(cell.Groups[1].Value)).ToArray();
                if (cells.Length != 4 || string.IsNullOrWhiteSpace(cells[2]))
                    throw new FormatException("TRA operation row format changed.");
                DateTimeOffset? time = DateTime.TryParseExact(cells[0], "yyyy/MM/dd HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? new DateTimeOffset(date, TimeSpan.FromHours(8)) : null;
                events.Add((line, cells[1], cells[2], cells[3], time));
            }
        }
        if (events.Count == 0) return [];

        // Combine the same bulletin repeated under multiple lines into one compact card.
        var summary = string.Join("；", events.GroupBy(item => (item.Reason, item.Recovery))
            .Select(group => $"{string.Join("、", group.Select(item => $"{item.Line}（{item.Section}）").Distinct())}：{group.Key.Reason} {group.Key.Recovery}"));
        return [new LocalAlertResponse("tra-current-disruptions", "transport", "台鐵營運中斷",
            "warning", "官方列有受阻路線", "台鐵部分路線停駛／受阻", summary,
            string.Join("、", events.Select(item => item.Line).Distinct()), "國營臺灣鐵路公司",
            events.Max(item => item.Time), null, null, SourceUrl)];
    }

    private static Regex MatchRegex(string pattern) => new(pattern,
        RegexOptions.IgnoreCase | RegexOptions.Singleline, TimeSpan.FromSeconds(2));

    private static string Text(string value) => Regex.Replace(
        WebUtility.HtmlDecode(MatchRegex(@"<[^>]*>").Replace(value, " ")),
        @"\s+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
}
