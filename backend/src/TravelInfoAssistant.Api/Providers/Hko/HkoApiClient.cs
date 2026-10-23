using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Hko;

public sealed class HkoApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<HkoOptions> options,
    TimeProvider timeProvider) : IHkoApiClient
{
    public async Task<HkoAlertBundle> GetAlertBundleAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("hko-api");
        var settings = options.Value;
        var fetchedAt = timeProvider.GetUtcNow();

        var warningSummaryTask = GetJsonAsync(
            client,
            settings.WarningSummaryPath,
            cancellationToken);
        var warningInfoTask = GetJsonAsync(
            client,
            settings.WarningInfoPath,
            cancellationToken);
        var specialWeatherTipsTask = GetJsonAsync(
            client,
            settings.SpecialWeatherTipsPath,
            cancellationToken);
        var feltEarthquakeTask = GetJsonAsync(
            client,
            settings.FeltEarthquakePath,
            cancellationToken);

        await Task.WhenAll(
            warningSummaryTask,
            warningInfoTask,
            specialWeatherTipsTask,
            feltEarthquakeTask);

        return new HkoAlertBundle(
            await warningSummaryTask,
            await warningInfoTask,
            await specialWeatherTipsTask,
            await feltEarthquakeTask,
            fetchedAt);
    }

    private async Task<string> GetJsonAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new HttpRequestException("HKO returned an empty JSON document.");
        }

        using var _ = JsonDocument.Parse(json);
        return json;
    }
}
