using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Boca;

public sealed class BocaApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<BocaOptions> options,
    TimeProvider timeProvider) : IBocaApiClient
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _lastXml;
    private EntityTagHeaderValue? _etag;
    private DateTimeOffset? _lastModified;
    private DateTimeOffset _lastFetchedAt;

    public async Task<BocaRssDocument> GetAlertsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var client = httpClientFactory.CreateClient("boca-api");
            using var request = new HttpRequestMessage(HttpMethod.Get, options.Value.RssPath);
            request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/rss+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            if (_etag is not null)
            {
                request.Headers.IfNoneMatch.Add(_etag);
            }

            if (_lastModified.HasValue)
            {
                request.Headers.IfModifiedSince = _lastModified;
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified && _lastXml is not null)
            {
                return new BocaRssDocument(_lastXml, _lastFetchedAt, _lastModified);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new BocaProviderException(
                    $"BOCA returned HTTP {(int)response.StatusCode}.",
                    (int)response.StatusCode);
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(xml))
            {
                throw new BocaProviderException("BOCA returned an empty RSS document.");
            }

            _lastXml = xml;
            _etag = response.Headers.ETag;
            _lastModified = response.Content.Headers.LastModified;
            _lastFetchedAt = timeProvider.GetUtcNow();
            return new BocaRssDocument(xml, _lastFetchedAt, _lastModified);
        }
        finally
        {
            _gate.Release();
        }
    }
}
