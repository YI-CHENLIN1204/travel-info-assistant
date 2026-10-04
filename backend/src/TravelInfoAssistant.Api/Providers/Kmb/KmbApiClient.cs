using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

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
    Task<KmbHttpResult<KmbGtfsSchedule>> GetScheduleAsync(CancellationToken cancellationToken);
}

public sealed class KmbApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<KmbOptions> options,
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

    public async Task<KmbHttpResult<KmbGtfsSchedule>> GetScheduleAsync(
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("kmb-schedule");
        using var response = await client.GetAsync(options.Value.ScheduleUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return new KmbHttpResult<KmbGtfsSchedule>(
            KmbGtfsParser.Parse(bytes),
            timeProvider.GetUtcNow(),
            response.Content.Headers.LastModified);
    }

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
