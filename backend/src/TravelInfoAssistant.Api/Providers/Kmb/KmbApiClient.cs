using System.Net.Http.Headers;
using System.Text.Json;

namespace TravelInfoAssistant.Api.Providers.Kmb;

public interface IKmbApiClient
{
    Task<KmbHttpResult<IReadOnlyList<KmbRouteRow>>> GetRoutesAsync(CancellationToken cancellationToken);
    Task<KmbHttpResult<IReadOnlyList<KmbRouteStopRow>>> GetRouteStopsAsync(CancellationToken cancellationToken);
    Task<KmbHttpResult<IReadOnlyList<KmbStopRow>>> GetStopsAsync(CancellationToken cancellationToken);
    Task<KmbHttpResult<IReadOnlyList<KmbEtaRow>>> GetEtaAsync(
        string stopId,
        string route,
        CancellationToken cancellationToken);
}

public sealed class KmbApiClient(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : IKmbApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<KmbHttpResult<IReadOnlyList<KmbRouteRow>>> GetRoutesAsync(
        CancellationToken cancellationToken) =>
        GetListAsync<KmbRouteRow>("route/", cancellationToken);

    public Task<KmbHttpResult<IReadOnlyList<KmbRouteStopRow>>> GetRouteStopsAsync(
        CancellationToken cancellationToken) =>
        GetListAsync<KmbRouteStopRow>("route-stop", cancellationToken);

    public Task<KmbHttpResult<IReadOnlyList<KmbStopRow>>> GetStopsAsync(
        CancellationToken cancellationToken) =>
        GetListAsync<KmbStopRow>("stop", cancellationToken);

    public Task<KmbHttpResult<IReadOnlyList<KmbEtaRow>>> GetEtaAsync(
        string stopId,
        string route,
        CancellationToken cancellationToken) =>
        GetListAsync<KmbEtaRow>(
            $"eta/{Uri.EscapeDataString(stopId)}/{Uri.EscapeDataString(route)}/1",
            cancellationToken);

    private async Task<KmbHttpResult<IReadOnlyList<T>>> GetListAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("kmb-api");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await JsonSerializer.DeserializeAsync<KmbResponse<IReadOnlyList<T>>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions,
            cancellationToken);
        if (payload?.Data is null)
        {
            throw new InvalidOperationException("KMB returned an empty response.");
        }

        return new KmbHttpResult<IReadOnlyList<T>>(
            payload.Data,
            timeProvider.GetUtcNow(),
            payload.GeneratedAt);
    }
}
