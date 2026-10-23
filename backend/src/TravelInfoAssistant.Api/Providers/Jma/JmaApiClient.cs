using System.Net.Http.Headers;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Jma;

public sealed class JmaApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<JmaOptions> options,
    TimeProvider timeProvider) : IJmaApiClient
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly TimeSpan RecentEarthquakeWindow = TimeSpan.FromHours(24);
    private const string WeatherTitle = "気象特別警報・警報・注意報";
    private const string LegacyWeatherTitle = "気象警報・注意報";
    private const string EarthquakeTitle = "震源・震度に関する情報";
    private const string TsunamiTitle = "津波警報・注意報・予報";

    private static readonly string[] VolcanoTitles =
    [
        "噴火警報・予報",
        "噴火速報",
        "火山の状況に関する解説情報"
    ];

    public async Task<JmaDocumentBundle> GetTokyoAlertDocumentsAsync(
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("jma-api");
        var settings = options.Value;
        var fetchedAt = timeProvider.GetUtcNow();

        var weatherFeedTask = GetStringAsync(client, settings.WeatherFeedPath, cancellationToken);
        var hazardFeedTask = GetStringAsync(
            client,
            settings.EarthquakeVolcanoFeedPath,
            cancellationToken);
        await Task.WhenAll(weatherFeedTask, hazardFeedTask);

        var weatherEntries = ParseEntries(await weatherFeedTask)
            .Where(entry =>
                (entry.Title is WeatherTitle or LegacyWeatherTitle) &&
                entry.Content.Contains("東京都", StringComparison.Ordinal))
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(1);

        var hazardEntries = ParseEntries(await hazardFeedTask).ToArray();
        var earthquakeEntries = hazardEntries
            .Where(entry =>
                entry.Title == EarthquakeTitle &&
                entry.UpdatedAt >= fetchedAt.Subtract(RecentEarthquakeWindow))
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(Math.Max(1, settings.MaxEarthquakeDocuments));
        var tsunamiEntries = hazardEntries
            .Where(entry => entry.Title == TsunamiTitle)
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(Math.Max(1, settings.MaxTsunamiDocuments));
        var volcanoEntries = hazardEntries
            .Where(entry => VolcanoTitles.Contains(entry.Title, StringComparer.Ordinal))
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(Math.Max(1, settings.MaxVolcanoDocuments));

        var selected = weatherEntries
            .Concat(earthquakeEntries)
            .Concat(tsunamiEntries)
            .Concat(volcanoEntries)
            .GroupBy(entry => entry.SourceUrl, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        using var gate = new SemaphoreSlim(8, 8);
        var documents = await Task.WhenAll(selected.Select(async entry =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var xml = await GetStringAsync(client, entry.SourceUrl, cancellationToken);
                return new JmaBulletinDocument(xml, entry.SourceUrl, entry.UpdatedAt);
            }
            finally
            {
                gate.Release();
            }
        }));

        return new JmaDocumentBundle(documents, fetchedAt);
    }

    private async Task<string> GetStringAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new HttpRequestException("JMA returned an empty XML document.");
        }

        return xml;
    }

    private static IEnumerable<JmaFeedEntry> ParseEntries(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.None);
        foreach (var entry in document.Root?.Elements(Atom + "entry") ?? [])
        {
            var title = entry.Element(Atom + "title")?.Value.Trim() ?? string.Empty;
            var content = entry.Element(Atom + "content")?.Value.Trim() ?? string.Empty;
            var sourceUrl = entry.Elements(Atom + "link")
                .FirstOrDefault(link =>
                    string.Equals(
                        (string?)link.Attribute("type"),
                        "application/xml",
                        StringComparison.OrdinalIgnoreCase))
                ?.Attribute("href")?.Value;
            var updatedText = entry.Element(Atom + "updated")?.Value;

            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(sourceUrl) ||
                !Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri) ||
                !string.Equals(sourceUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(sourceUri.Host, "www.data.jma.go.jp", StringComparison.OrdinalIgnoreCase) ||
                !DateTimeOffset.TryParse(updatedText, out var updatedAt))
            {
                continue;
            }

            yield return new JmaFeedEntry(title, content, sourceUri.AbsoluteUri, updatedAt);
        }
    }

    private sealed record JmaFeedEntry(
        string Title,
        string Content,
        string SourceUrl,
        DateTimeOffset UpdatedAt);
}
