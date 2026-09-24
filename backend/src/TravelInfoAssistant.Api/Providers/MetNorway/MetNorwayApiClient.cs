using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public sealed record MetNorwayHttpResult(
    MetNorwayForecast Data,
    DateTimeOffset? LastModified,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset FetchedAt);

public interface IMetNorwayApiClient
{
    Task<MetNorwayHttpResult> GetForecastAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}

public sealed class MetNorwayApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<MetNorwayOptions> options,
    IMetNorwayRateGate rateGate,
    TimeProvider timeProvider) : IMetNorwayApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<MetNorwayHttpResult> GetForecastAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        await rateGate.WaitAsync(cancellationToken);

        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"compact?lat={latitude:0.####}&lon={longitude:0.####}");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", options.Value.UserAgent);

        var client = httpClientFactory.CreateClient("met-norway-api");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            throw new MetNorwayProviderException(
                "MET Norway rate limit reached.",
                (int)response.StatusCode);
        }

        if (response.StatusCode is HttpStatusCode.Forbidden)
        {
            throw new MetNorwayProviderException(
                "MET Norway rejected the configured User-Agent.",
                (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new MetNorwayProviderException(
                $"MET Norway returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<MetNorwayForecast>(bytes, JsonOptions)
            ?? throw new MetNorwayProviderException("MET Norway returned an empty response.");
        if (data.Properties?.Timeseries.Count is not > 0)
        {
            throw new MetNorwayProviderException("MET Norway returned no forecast points.");
        }

        return new MetNorwayHttpResult(
            data,
            response.Content.Headers.LastModified,
            response.Content.Headers.Expires,
            timeProvider.GetUtcNow());
    }
}
