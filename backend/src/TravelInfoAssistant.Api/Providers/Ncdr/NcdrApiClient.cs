using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Ncdr;

public sealed class NcdrApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<NcdrOptions> options,
    TimeProvider timeProvider) : INcdrApiClient
{
    public async Task<NcdrAtomDocument> GetActiveAlertsAsync(
        string county,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("ncdr-api");
        var path = $"{options.Value.ActiveFeedPath}?County={Uri.EscapeDataString(county)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/atom+xml"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new HttpRequestException("NCDR returned an empty Atom document.");
        }

        return new NcdrAtomDocument(xml, timeProvider.GetUtcNow());
    }
}
