using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public sealed record AeroDataBoxHttpResult<T>(
    T? Data,
    DateTimeOffset? LastModified,
    DateTimeOffset FetchedAt,
    long ResponseBytes);

public interface IAeroDataBoxApiClient
{
    Task<AeroDataBoxHttpResult<T>> GetAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        AeroDataBoxRequestKind kind,
        CancellationToken cancellationToken);
}

public sealed class AeroDataBoxApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AeroDataBoxOptions> options,
    IAeroDataBoxRateGate rateGate,
    IAeroDataBoxUsageMeter usageMeter,
    TimeProvider timeProvider) : IAeroDataBoxApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AeroDataBoxHttpResult<T>> GetAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        AeroDataBoxRequestKind kind,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new AeroDataBoxNotConfiguredException();
        }

        if (!await usageMeter.TryReserveAsync(
                kind,
                settings.Tier2RequestUnits,
                cancellationToken))
        {
            throw new AeroDataBoxQuotaExceededException();
        }

        await rateGate.WaitAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(relativePath, query));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (settings.IsDirect)
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", settings.ApiKey);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("X-RapidAPI-Key", settings.ApiKey);
            request.Headers.TryAddWithoutValidation("X-RapidAPI-Host", settings.RapidApiHost);
        }

        var client = httpClientFactory.CreateClient("aerodatabox-api");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await usageMeter.AddResponseBytesAsync(bytes.LongLength, cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            throw new AeroDataBoxQuotaExceededException();
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new AeroDataBoxProviderException(
                "AeroDataBox rejected the configured API key.",
                (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new AeroDataBoxProviderException(
                $"AeroDataBox returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        var fetchedAt = timeProvider.GetUtcNow();
        if (response.StatusCode == HttpStatusCode.NoContent || bytes.Length == 0)
        {
            return new AeroDataBoxHttpResult<T>(
                default,
                response.Content.Headers.LastModified,
                fetchedAt,
                bytes.LongLength);
        }

        var data = JsonSerializer.Deserialize<T>(bytes, JsonOptions)
            ?? throw new AeroDataBoxProviderException("AeroDataBox returned an empty data response.");
        return new AeroDataBoxHttpResult<T>(
            data,
            response.Content.Headers.LastModified,
            fetchedAt,
            bytes.LongLength);
    }

    private static string BuildRequestUri(
        string relativePath,
        IReadOnlyDictionary<string, string?> query)
    {
        var values = query
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}");
        var queryText = string.Join('&', values);
        return string.IsNullOrEmpty(queryText) ? relativePath : $"{relativePath}?{queryText}";
    }
}
