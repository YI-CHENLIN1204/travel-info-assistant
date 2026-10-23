using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Mss;

public static partial class MssAlertMapper
{
    public const string SourceUrl = "https://www.weather.gov.sg/warning-heavy-rain/";
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public static IReadOnlyList<LocalAlertResponse> Map(string xml, DateTimeOffset now)
    {
        var document = XDocument.Parse(xml);
        if (document.Root?.Name != Atom + "feed")
        {
            throw new FormatException("MSS returned an unexpected warning feed.");
        }

        var latest = document.Root.Elements(Atom + "entry")
            .Select(entry => new
            {
                Entry = entry,
                Updated = ParseDate(entry.Element(Atom + "updated")?.Value)
            })
            .OrderByDescending(item => item.Updated)
            .FirstOrDefault();
        if (latest is null)
        {
            return [];
        }

        var summary = WebUtility.HtmlDecode(
            HtmlTags().Replace(latest.Entry.Element(Atom + "summary")?.Value ?? "", " ")).Trim();
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new FormatException("MSS warning summary is missing.");
        }

        if (summary.Equals("NIL", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("no heavy rain warning", StringComparison.OrdinalIgnoreCase) ||
            summary.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        // MSS describes validity in Singapore local time. Require an explicit end time:
        // an old bulletin must never become an indefinitely active warning.
        var end = EndTime().Match(summary);
        if (!latest.Updated.HasValue || !end.Success ||
            !DateTime.TryParseExact(end.Groups[1].Value, "HHmm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var clock))
        {
            throw new FormatException("MSS warning validity could not be determined.");
        }

        var local = latest.Updated.Value.ToOffset(TimeSpan.FromHours(8));
        var expires = new DateTimeOffset(local.Year, local.Month, local.Day,
            clock.Hour, clock.Minute, 0, TimeSpan.FromHours(8));
        if (expires < local)
        {
            expires = expires.AddDays(1);
        }

        if (expires <= now || latest.Updated.Value < now.AddHours(-24))
        {
            return [];
        }

        return [new LocalAlertResponse(
            latest.Entry.Element(Atom + "id")?.Value ?? $"mss-rain-{latest.Updated:O}",
            "weather", "豪雨警告", "warning", "官方豪雨警告",
            "新加坡豪雨警告", summary, "新加坡", "新加坡氣象署 MSS",
            latest.Updated, latest.Updated, expires, SourceUrl)];
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed) ? parsed : null;

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTags();

    [GeneratedRegex(@"(?:to|till|until)\s+(\d{4})\s*(?:h|hr|hrs)", RegexOptions.IgnoreCase)]
    private static partial Regex EndTime();
}
