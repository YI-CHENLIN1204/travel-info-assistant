using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public sealed class OdptTransitProvider(
    IOdptApiClient apiClient,
    IProviderCache cache,
    IOptions<OdptOptions> options,
    TimeProvider timeProvider,
    ILogger<OdptTransitProvider> logger) : IOdptTransitProvider
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan TimetableFreshFor = TimeSpan.FromHours(12);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(7);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Unavailable<TransitRouteResponse>("ODPT 金鑰尚未設定。");
        }

        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<TransitRouteResponse>>(
                "transit:odpt:tokyo-metro:routes:v1",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetRailwaysAsync(token);
                    var data = response.Data
                        .Select(OdptTransitMapper.MapRailway)
                        .Where(item => item is not null)
                        .Cast<TransitRouteResponse>()
                        .OrderBy(item => item.NameZh)
                        .ToList();
                    return new ProviderPayload<IReadOnlyList<TransitRouteResponse>>(
                        data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);
            return result with { Source = "ODPT" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo Metro route query failed.");
            return Unavailable<TransitRouteResponse>(GetPublicErrorMessage(exception));
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Unavailable<MetroStationResponse>("ODPT 金鑰尚未設定。");
        }

        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<MetroStationResponse>>(
                "transit:odpt:tokyo-metro:stations:v1",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetStationsAsync(token);
                    var data = response.Data
                        .Select(OdptTransitMapper.MapStation)
                        .Where(item => item is not null)
                        .Cast<MetroStationResponse>()
                        .OrderBy(item => item.NameZh)
                        .ThenBy(item => item.Code)
                        .ToList();
                    return new ProviderPayload<IReadOnlyList<MetroStationResponse>>(
                        data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);
            return result with { Source = "ODPT" };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo Metro station query failed.");
            return Unavailable<MetroStationResponse>(GetPublicErrorMessage(exception));
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroDeparturesAsync(
        string stationId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Unavailable<TransitArrivalResponse>("ODPT 金鑰尚未設定。");
        }

        try
        {
            ProviderQueryResult<IReadOnlyList<OdptCalendar>>? calendarFeed = null;
            try
            {
                calendarFeed = await GetCalendarsAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "ODPT calendar query failed; using weekday fallback.");
            }

            var timetableFeed = await cache.GetOrCreateAsync<IReadOnlyList<OdptStationTimetable>>(
                $"transit:odpt:tokyo-metro:timetable:v1:{stationId.ToLowerInvariant()}",
                TimetableFreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetStationTimetablesAsync(stationId, token);
                    return new ProviderPayload<IReadOnlyList<OdptStationTimetable>>(
                        response.Data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);
            var stationFeed = await GetMetroStationsAsync(cancellationToken);
            var stationNames = stationFeed.Data
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().NameZh,
                    StringComparer.OrdinalIgnoreCase);
            var departures = OdptTransitMapper.MapStationDepartures(
                timetableFeed.Data,
                calendarFeed?.Data ?? [],
                stationNames,
                timeProvider.GetUtcNow());
            var message = timetableFeed.Message ?? calendarFeed?.Message ?? stationFeed.Message;
            if (departures.Count == 0 && string.IsNullOrWhiteSpace(message))
            {
                message = "目前查無接下來的表定班次。";
            }

            return new ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>(
                departures,
                timetableFeed.DataStatus,
                timetableFeed.SourceUpdatedAt,
                timetableFeed.FetchedAt,
                timetableFeed.Stale,
                message,
                "ODPT");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo Metro timetable query failed.");
            return Unavailable<TransitArrivalResponse>(GetPublicErrorMessage(exception));
        }
    }

    private Task<ProviderQueryResult<IReadOnlyList<OdptCalendar>>> GetCalendarsAsync(
        CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync<IReadOnlyList<OdptCalendar>>(
            "transit:odpt:calendars:v1",
            FreshFor,
            RetainFor,
            async token =>
            {
                var response = await apiClient.GetCalendarsAsync(token);
                return new ProviderPayload<IReadOnlyList<OdptCalendar>>(
                    response.Data,
                    "scheduled",
                    Latest(response.Data.Select(item => item.UpdatedAt)),
                    response.FetchedAt);
            },
            cancellationToken);

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>(string message) =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            Array.Empty<T>(),
            message,
            timeProvider,
            "ODPT");

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values) =>
        values.Where(value => value.HasValue).Max();

    private static string GetPublicErrorMessage(Exception exception) => exception switch
    {
        OdptNotConfiguredException => "ODPT 金鑰尚未設定。",
        OdptProviderException { StatusCode: 401 or 403 } => "ODPT 金鑰無效或尚未取得資料權限。",
        OdptProviderException { StatusCode: 429 } => "ODPT 查詢次數已達限制，請稍後再試。",
        OdptProviderException => "ODPT 暫時無法提供東京地鐵資料，請稍後再試。",
        HttpRequestException => "目前無法連線至 ODPT，請稍後再試。",
        _ => "東京地鐵資料暫時無法更新，請稍後再試。"
    };
}
