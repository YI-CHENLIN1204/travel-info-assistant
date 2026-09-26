using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public sealed record OdptHttpResult<T>(T Data, DateTimeOffset FetchedAt);

public interface IOdptApiClient
{
    Task<OdptHttpResult<IReadOnlyList<OdptCalendar>>> GetCalendarsAsync(
        CancellationToken cancellationToken);

    Task<OdptHttpResult<IReadOnlyList<OdptRailway>>> GetRailwaysAsync(
        CancellationToken cancellationToken);

    Task<OdptHttpResult<IReadOnlyList<OdptStation>>> GetStationsAsync(
        CancellationToken cancellationToken);

    Task<OdptHttpResult<IReadOnlyList<OdptStationTimetable>>> GetStationTimetablesAsync(
        string stationId,
        CancellationToken cancellationToken);
}

public sealed class OdptApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OdptOptions> options,
    TimeProvider timeProvider) : IOdptApiClient
{
    private const string TokyoMetroOperator = "odpt.Operator:TokyoMetro";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<OdptHttpResult<IReadOnlyList<OdptCalendar>>> GetCalendarsAsync(
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<OdptCalendar>>(
            "odpt:Calendar",
            new Dictionary<string, string?>(),
            cancellationToken);

    public Task<OdptHttpResult<IReadOnlyList<OdptRailway>>> GetRailwaysAsync(
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<OdptRailway>>(
            "odpt:Railway",
            new Dictionary<string, string?>
            {
                ["odpt:operator"] = TokyoMetroOperator
            },
            cancellationToken);

    public Task<OdptHttpResult<IReadOnlyList<OdptStation>>> GetStationsAsync(
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<OdptStation>>(
            "odpt:Station",
            new Dictionary<string, string?>
            {
                ["odpt:operator"] = TokyoMetroOperator
            },
            cancellationToken);

    public Task<OdptHttpResult<IReadOnlyList<OdptStationTimetable>>> GetStationTimetablesAsync(
        string stationId,
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<OdptStationTimetable>>(
            "odpt:StationTimetable",
            new Dictionary<string, string?>
            {
                ["odpt:operator"] = TokyoMetroOperator,
                ["odpt:station"] = stationId
            },
            cancellationToken);

    private async Task<OdptHttpResult<T>> GetAsync<T>(
        string relativePath,
        IReadOnlyDictionary<string, string?> query,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new OdptNotConfiguredException();
        }

        var values = query
            .Append(new KeyValuePair<string, string?>("acl:consumerKey", settings.ConsumerKey))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item =>
                $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value!)}");
        var requestUri = $"{relativePath}?{string.Join('&', values)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = httpClientFactory.CreateClient("odpt-api");
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new OdptProviderException(
                "ODPT rejected the configured consumer key.",
                (int)response.StatusCode);
        }

        if (response.StatusCode is HttpStatusCode.TooManyRequests)
        {
            throw new OdptProviderException("ODPT rate limit reached.", (int)response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OdptProviderException(
                $"ODPT returned HTTP {(int)response.StatusCode}.",
                (int)response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<T>(bytes, JsonOptions)
            ?? throw new OdptProviderException("ODPT returned an empty data response.");

        return new OdptHttpResult<T>(data, timeProvider.GetUtcNow());
    }
}
