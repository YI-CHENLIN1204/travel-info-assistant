using System.Net.Http.Headers;
using System.Text.Json;

namespace TravelInfoAssistant.Api.Providers.Nlb;

public interface INlbApiClient
{
    Task<NlbHttpResult<IReadOnlyList<NlbRouteRow>>> GetRoutesAsync(
        CancellationToken cancellationToken);
    Task<NlbHttpResult<IReadOnlyList<NlbStopRow>>> GetStopsAsync(
        string routeId,
        CancellationToken cancellationToken);
    Task<NlbHttpResult<IReadOnlyList<NlbEtaRow>>> GetEtaAsync(
        string routeId,
        string stopId,
        CancellationToken cancellationToken);
}

public sealed class NlbApiClient(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : INlbApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<NlbHttpResult<IReadOnlyList<NlbRouteRow>>> GetRoutesAsync(
        CancellationToken cancellationToken)
    {
        var payload = await GetAsync<NlbRoutesResponse>("route.php?action=list", cancellationToken);
        return new NlbHttpResult<IReadOnlyList<NlbRouteRow>>(payload.Routes, timeProvider.GetUtcNow());
    }

    public async Task<NlbHttpResult<IReadOnlyList<NlbStopRow>>> GetStopsAsync(
        string routeId,
        CancellationToken cancellationToken)
    {
        var payload = await GetAsync<NlbStopsResponse>(
            $"stop.php?action=list&routeId={Uri.EscapeDataString(routeId)}",
            cancellationToken);
        return new NlbHttpResult<IReadOnlyList<NlbStopRow>>(payload.Stops, timeProvider.GetUtcNow());
    }

    public async Task<NlbHttpResult<IReadOnlyList<NlbEtaRow>>> GetEtaAsync(
        string routeId,
        string stopId,
        CancellationToken cancellationToken)
    {
        var payload = await GetAsync<NlbEtaResponse>(
            $"stop.php?action=estimatedArrivals&routeId={Uri.EscapeDataString(routeId)}" +
            $"&stopId={Uri.EscapeDataString(stopId)}&language=zh",
            cancellationToken);
        return new NlbHttpResult<IReadOnlyList<NlbEtaRow>>(
            payload.EstimatedArrivals,
            timeProvider.GetUtcNow());
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        var client = httpClientFactory.CreateClient("nlb-api");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await JsonSerializer.DeserializeAsync<T>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions,
            cancellationToken);
        return payload ?? throw new InvalidOperationException("NLB returned an empty response.");
    }
}
