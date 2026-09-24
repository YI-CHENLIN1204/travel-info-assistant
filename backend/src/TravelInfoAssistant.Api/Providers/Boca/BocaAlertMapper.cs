using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Boca;

public static partial class BocaAlertMapper
{
    private static readonly CountryProfile[] Countries =
    [
        new("JP", "日本", "Japan"),
        new("KR", "韓國", "Korea"),
        new("TH", "泰國", "Thailand"),
        new("US", "美國", "United States"),
        new("FR", "法國", "France"),
        new("GB", "英國", "United Kingdom"),
        new("SG", "新加坡", "Singapore"),
        new("AU", "澳大利亞", "Australia")
    ];

    public static IReadOnlyList<TravelAlertResponse> Map(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        return document
            .Descendants("item")
            .Select(MapItem)
            .Where(item => item is not null)
            .Cast<TravelAlertResponse>()
            .OrderByDescending(item => item.Level)
            .ThenByDescending(item => item.UpdatedAt)
            .ToArray();
    }

    private static TravelAlertResponse? MapItem(XElement item)
    {
        var title = Normalize(item.Element("title")?.Value);
        var levelMatch = LevelPattern().Match(title);
        if (!levelMatch.Success)
        {
            return null;
        }

        var level = levelMatch.Groups["level"].Value switch
        {
            "一" => 1,
            "二" => 2,
            "三" => 3,
            "四" => 4,
            _ => 0
        };
        if (level == 0)
        {
            return null;
        }

        var country = Countries.FirstOrDefault(profile =>
            title.Contains(profile.NameZh, StringComparison.OrdinalIgnoreCase) ||
            title.Contains(profile.NameEn, StringComparison.OrdinalIgnoreCase));
        var remainder = title[levelMatch.Length..].Trim().TrimStart('-').Trim();
        var parts = remainder.Split(" - ", StringSplitOptions.RemoveEmptyEntries |
                                            StringSplitOptions.TrimEntries);
        var region = parts.FirstOrDefault() ?? country?.NameZh ?? "未標示地區";
        var countryNameZh = country?.NameZh ?? region;
        var countryNameEn = country?.NameEn ?? parts.LastOrDefault() ?? string.Empty;
        var description = ToPlainText(item.Element("description")?.Value);
        var sourceUrl = Normalize(item.Element("link")?.Value);
        var id = Normalize(item.Element("NewsID")?.Value);

        return new TravelAlertResponse(
            string.IsNullOrWhiteSpace(id) ? sourceUrl : id,
            level,
            levelMatch.Value.Trim(),
            country?.Code ?? "ZZ",
            countryNameZh,
            countryNameEn,
            region,
            Truncate(description, 260),
            ParseDate(item.Element("pubDate")?.Value),
            sourceUrl);
    }

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.ToUniversalTime()
            : null;

    private static string ToPlainText(string? html)
    {
        var withoutTags = HtmlTagPattern().Replace(html ?? string.Empty, " ");
        return Normalize(WebUtility.HtmlDecode(withoutTags));
    }

    private static string Normalize(string? value) =>
        WhitespacePattern().Replace(value ?? string.Empty, " ").Trim();

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : string.Concat(value.AsSpan(0, maximumLength), "…");

    [GeneratedRegex("第(?<level>[一二三四])級[：:]?[^-]*", RegexOptions.CultureInvariant)]
    private static partial Regex LevelPattern();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();

    private sealed record CountryProfile(string Code, string NameZh, string NameEn);
}
