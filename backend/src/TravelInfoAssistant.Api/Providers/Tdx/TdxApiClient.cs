using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public sealed record TdxHttpResult<T>(
    T Data,
    DateTimeOffset? LastModified,
    DateTimeOffset FetchedAt,
    long ResponseBytes);

public interface ITdxApiClient
{
    Task<TdxHttpResult<T>> GetAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken);
}

public sealed class TdxApiClient(
    IHttpClientFactory httpClientFactory,
    ITdxTokenProvider tokenProvider,
    ITdxRateGate rateGate,
    ITdxUsageMeter usageMeter,
    TimeProvider timeProvider) : ITdxApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TdxHttpResult<T>> GetAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        if (!rateGate.TryAcquire())
        {
            throw new TdxRateLimitException();
        }

        if (!await usageMeter.TryReserveRequestAsync(cancellationToken))
        {
            throw new TdxQuotaExceededException();
        }

        var requestUri = BuildRequestUri(relativePath, query);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = httpClientFactory.CreateClient("tdx-api");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        await usageMeter.AddResponseBytesAsync(bytes.LongLength, cancellationToken);

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            throw new TdxRateLimitException();
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            tokenProvider.Invalidate();
            throw new TdxProviderException("TDX rejected the access token.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new TdxProviderException(
                $"TDX returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        var data = JsonSerializer.Deserialize<T>(bytes, JsonOptions)
            ?? throw new TdxProviderException("TDX returned an empty data response.");

        var lastModified = response.Content.Headers.LastModified;
        return new TdxHttpResult<T>(
            data,
            lastModified,
            timeProvider.GetUtcNow(),
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
