using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Citybus;

public interface ICitybusApiClient
{
    Task<CitybusHttpResult<IReadOnlyList<CitybusRouteRow>>> GetRoutesAsync(
        CancellationToken cancellationToken);
    Task<CitybusHttpResult<IReadOnlyList<CitybusRouteStopRow>>> GetRouteStopsAsync(
        string route,
        int direction,
        CancellationToken cancellationToken);
    Task<CitybusHttpResult<CitybusStopRow>> GetStopAsync(
        string stopId,
        CancellationToken cancellationToken);
    Task<CitybusHttpResult<IReadOnlyList<CitybusEtaRow>>> GetEtaAsync(
        string stopId,
        string route,
        CancellationToken cancellationToken);
}

public sealed class CitybusApiClient(
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider) : ICitybusApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<CitybusHttpResult<IReadOnlyList<CitybusRouteRow>>> GetRoutesAsync(
        CancellationToken cancellationToken) =>
        GetListAsync<CitybusRouteRow>("route/ctb", cancellationToken);

    public Task<CitybusHttpResult<IReadOnlyList<CitybusRouteStopRow>>> GetRouteStopsAsync(
        string route,
        int direction,
        CancellationToken cancellationToken) =>
        GetListAsync<CitybusRouteStopRow>(
            $"route-stop/ctb/{Uri.EscapeDataString(route)}/{(direction == 0 ? "outbound" : "inbound")}",
            cancellationToken);

    public Task<CitybusHttpResult<CitybusStopRow>> GetStopAsync(
        string stopId,
        CancellationToken cancellationToken) =>
        GetItemAsync<CitybusStopRow>($"stop/{Uri.EscapeDataString(stopId)}", cancellationToken);

    public Task<CitybusHttpResult<IReadOnlyList<CitybusEtaRow>>> GetEtaAsync(
        string stopId,
        string route,
        CancellationToken cancellationToken) =>
        GetListAsync<CitybusEtaRow>(
            $"eta/ctb/{Uri.EscapeDataString(stopId)}/{Uri.EscapeDataString(route)}",
            cancellationToken);

    private async Task<CitybusHttpResult<IReadOnlyList<T>>> GetListAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("citybus-api");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await JsonSerializer.DeserializeAsync<CitybusResponse<IReadOnlyList<T>>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions,
            cancellationToken);
        if (payload?.Data is null)
        {
            throw new InvalidOperationException("Citybus returned an empty response.");
        }

        return new CitybusHttpResult<IReadOnlyList<T>>(
            payload.Data,
            timeProvider.GetUtcNow(),
            LatestTimestamp(payload.Data));
    }

    private async Task<CitybusHttpResult<T>> GetItemAsync<T>(
        string path,
        CancellationToken cancellationToken)
        where T : class
    {
        var client = httpClientFactory.CreateClient("citybus-api");
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await JsonSerializer.DeserializeAsync<CitybusResponse<T>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            JsonOptions,
            cancellationToken);
        if (payload?.Data is null)
        {
            throw new InvalidOperationException("Citybus returned an empty response.");
        }

        var timestamp = payload.Data switch
        {
            CitybusStopRow value => value.DataTimestamp,
            _ => null
        };
        return new CitybusHttpResult<T>(payload.Data, timeProvider.GetUtcNow(), timestamp);
    }

    private static DateTimeOffset? LatestTimestamp<T>(IReadOnlyList<T> rows) =>
        rows.Select(item => item switch
            {
                CitybusRouteRow value => value.DataTimestamp,
                CitybusRouteStopRow value => value.DataTimestamp,
                CitybusEtaRow value => value.DataTimestamp,
                _ => null
            })
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max() is var value && value != default ? value : null;
}
