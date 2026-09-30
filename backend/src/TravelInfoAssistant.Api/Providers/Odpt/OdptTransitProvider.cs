using System.Security.Cryptography;
using System.Text;
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
    private const string TokyoMetroOperatorId = "odpt.Operator:TokyoMetro";
    private const string ToeiOperatorId = "odpt.Operator:Toei";
    private static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan TimetableFreshFor = TimeSpan.FromHours(12);
    private static readonly TimeSpan StatusFreshFor = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StatusRetainFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(7);
    private static readonly SubwayOperator[] TokyoSubwayOperators =
    [
        new(TokyoMetroOperatorId, "tokyo-metro", "Tokyo Metro"),
        new(ToeiOperatorId, "toei", "都營地下鐵")
    ];
    private static readonly HashSet<string> SupportedToeiRailways = new(
        [
            "odpt.Railway:Toei.Asakusa",
            "odpt.Railway:Toei.Mita",
            "odpt.Railway:Toei.Shinjuku",
            "odpt.Railway:Toei.Oedo"
        ],
        StringComparer.OrdinalIgnoreCase);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Unavailable<TransitRouteResponse>("ODPT 金鑰尚未設定。");
        }

        try
        {
            var feeds = await Task.WhenAll(TokyoSubwayOperators.Select(
                subwayOperator => GetRailwayFeedAsync(subwayOperator, cancellationToken)));
            var data = feeds
                .SelectMany(feed => feed.Result.Data)
                .Where(item => IsSupportedRailway(item.SameAs))
                .Select(OdptTransitMapper.MapRailway)
                .Where(item => item is not null)
                .Cast<TransitRouteResponse>()
                .OrderBy(item => item.Operators.FirstOrDefault())
                .ThenBy(item => item.NameZh)
                .ToList();
            return CombineFeeds(
                feeds,
                data,
                "scheduled",
                "目前查無可顯示的東京地下鐵路線。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo subway route query failed.");
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
            var feeds = await Task.WhenAll(TokyoSubwayOperators.Select(
                subwayOperator => GetStationFeedAsync(subwayOperator, cancellationToken)));
            var data = feeds
                .SelectMany(feed => feed.Result.Data)
                .Where(item => IsSupportedRailway(item.Railway))
                .Select(OdptTransitMapper.MapStation)
                .Where(item => item is not null)
                .Cast<MetroStationResponse>()
                .OrderBy(item => item.NameZh)
                .ThenBy(item => item.Code)
                .ToList();
            return CombineFeeds(
                feeds,
                data,
                "scheduled",
                "目前查無可顯示的東京地下鐵車站。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo subway station query failed.");
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

        var subwayOperator = GetSubwayOperatorForStation(stationId);
        if (subwayOperator is null)
        {
            return Unavailable<TransitArrivalResponse>("此車站不在目前支援的東京地下鐵範圍內。");
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
                $"transit:odpt:tokyo-subway:timetable:v1:{stationId.ToLowerInvariant()}",
                TimetableFreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetStationTimetablesAsync(
                        subwayOperator.Id,
                        stationId,
                        token);
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
            await AddReferencedStationNamesAsync(
                timetableFeed.Data,
                stationNames,
                cancellationToken);
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
            logger.LogWarning(exception, "ODPT Tokyo subway timetable query failed.");
            return Unavailable<TransitArrivalResponse>(GetPublicErrorMessage(exception));
        }
    }

    public async Task<ProviderQueryResult<IReadOnlyList<MetroServiceStatusResponse>>> GetMetroStatusAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return Unavailable<MetroServiceStatusResponse>("ODPT 尚未設定，暫時無法確認運行狀態。");
        }

        try
        {
            var feeds = await Task.WhenAll(TokyoSubwayOperators.Select(
                subwayOperator => GetTrainInformationFeedAsync(
                    subwayOperator,
                    cancellationToken)));
            var data = feeds
                .SelectMany(feed => feed.Result.Data
                    .Where(item => IsSupportedRailway(item.Railway))
                    .Select(item => OdptTransitMapper.MapTrainInformation(
                        item,
                        feed.Result.FetchedAt)))
                .Where(item => item is not null)
                .Cast<MetroServiceStatusResponse>()
                .OrderBy(item => item.LineName)
                .ToList();
            return CombineFeeds(
                feeds,
                data,
                "realtime",
                "目前沒有可顯示的東京地下鐵官方運行狀態。");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ODPT Tokyo subway status query failed.");
            return Unavailable<MetroServiceStatusResponse>(GetPublicErrorMessage(exception));
        }
    }

    private async Task<OperatorFeed<OdptRailway>> GetRailwayFeedAsync(
        SubwayOperator subwayOperator,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<OdptRailway>>(
                $"transit:odpt:tokyo-subway:routes:v1:{subwayOperator.CacheKey}",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetRailwaysAsync(subwayOperator.Id, token);
                    return new ProviderPayload<IReadOnlyList<OdptRailway>>(
                        response.Data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);
            return new OperatorFeed<OdptRailway>(
                subwayOperator,
                result with { Source = "ODPT" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "ODPT route query failed for {Operator}.",
                subwayOperator.Id);
            return new OperatorFeed<OdptRailway>(
                subwayOperator,
                Unavailable<OdptRailway>(GetPublicErrorMessage(exception)));
        }
    }

    private async Task<OperatorFeed<OdptStation>> GetStationFeedAsync(
        SubwayOperator subwayOperator,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<OdptStation>>(
                $"transit:odpt:tokyo-subway:stations:v1:{subwayOperator.CacheKey}",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetStationsAsync(subwayOperator.Id, token);
                    return new ProviderPayload<IReadOnlyList<OdptStation>>(
                        response.Data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);
            return new OperatorFeed<OdptStation>(
                subwayOperator,
                result with { Source = "ODPT" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "ODPT station query failed for {Operator}.",
                subwayOperator.Id);
            return new OperatorFeed<OdptStation>(
                subwayOperator,
                Unavailable<OdptStation>(GetPublicErrorMessage(exception)));
        }
    }

    private async Task<OperatorFeed<OdptTrainInformation>> GetTrainInformationFeedAsync(
        SubwayOperator subwayOperator,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<OdptTrainInformation>>(
                $"transit:odpt:tokyo-subway:status:v1:{subwayOperator.CacheKey}",
                StatusFreshFor,
                StatusRetainFor,
                async token =>
                {
                    var response = await apiClient.GetTrainInformationAsync(
                        subwayOperator.Id,
                        token);
                    return new ProviderPayload<IReadOnlyList<OdptTrainInformation>>(
                        response.Data,
                        "realtime",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt,
                        EarliestFuture(
                            response.Data.Select(item => item.ValidUntil),
                            response.FetchedAt));
                },
                cancellationToken);

            if (result.Stale)
            {
                result = Unavailable<OdptTrainInformation>(
                    "ODPT 運行狀態已過期，暫時無法確認。");
            }

            return new OperatorFeed<OdptTrainInformation>(
                subwayOperator,
                result with { Source = "ODPT" });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "ODPT status query failed for {Operator}.",
                subwayOperator.Id);
            return new OperatorFeed<OdptTrainInformation>(
                subwayOperator,
                Unavailable<OdptTrainInformation>(GetPublicErrorMessage(exception)));
        }
    }

    private async Task AddReferencedStationNamesAsync(
        IReadOnlyList<OdptStationTimetable> timetables,
        IDictionary<string, string> stationNames,
        CancellationToken cancellationToken)
    {
        var missingStationIds = timetables
            .SelectMany(timetable => timetable.Objects)
            .SelectMany(item => item.DestinationStations)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item) && !stationNames.ContainsKey(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingStationIds.Count == 0)
        {
            return;
        }

        try
        {
            var lookupKey = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join('\n', missingStationIds))));
            var stationFeed = await cache.GetOrCreateAsync<IReadOnlyList<OdptStation>>(
                $"transit:odpt:referenced-stations:v1:{lookupKey}",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetStationsByIdsAsync(
                        missingStationIds,
                        token);
                    return new ProviderPayload<IReadOnlyList<OdptStation>>(
                        response.Data,
                        "scheduled",
                        Latest(response.Data.Select(item => item.UpdatedAt)),
                        response.FetchedAt);
                },
                cancellationToken);

            foreach (var station in stationFeed.Data)
            {
                var id = station.SameAs?.Trim();
                var name = station.StationTitle?.Ja?.Trim() ?? station.Title?.Trim();
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                {
                    stationNames[id] = name;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "ODPT referenced station name query failed; using identifier fallback.");
        }
    }

    private ProviderQueryResult<IReadOnlyList<TOutput>> CombineFeeds<TInput, TOutput>(
        IReadOnlyList<OperatorFeed<TInput>> feeds,
        IReadOnlyList<TOutput> data,
        string dataStatus,
        string emptyMessage)
    {
        var usableFeeds = feeds
            .Where(feed => feed.Result.DataStatus != "unavailable")
            .ToList();
        var messages = feeds
            .Where(feed => !string.IsNullOrWhiteSpace(feed.Result.Message))
            .Select(feed => $"{feed.Operator.DisplayName}：{feed.Result.Message}")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var message = messages.Count > 0
            ? string.Join(" ", messages)
            : data.Count == 0 ? emptyMessage : null;
        var combinedStatus = usableFeeds.Count == 0
            ? "unavailable"
            : usableFeeds.All(feed => feed.Result.DataStatus == "cached")
                ? "cached"
                : dataStatus;

        return new ProviderQueryResult<IReadOnlyList<TOutput>>(
            data,
            combinedStatus,
            Latest(feeds.Select(feed => feed.Result.SourceUpdatedAt)),
            feeds.Max(feed => feed.Result.FetchedAt),
            usableFeeds.Any(feed => feed.Result.Stale),
            message,
            "ODPT");
    }

    private static bool IsSupportedRailway(string? railwayId) =>
        railwayId?.StartsWith(
            "odpt.Railway:TokyoMetro.",
            StringComparison.OrdinalIgnoreCase) == true ||
        (!string.IsNullOrWhiteSpace(railwayId) && SupportedToeiRailways.Contains(railwayId));

    private static SubwayOperator? GetSubwayOperatorForStation(string stationId)
    {
        if (stationId.StartsWith(
                "odpt.Station:TokyoMetro.",
                StringComparison.OrdinalIgnoreCase))
        {
            return TokyoSubwayOperators[0];
        }

        return SupportedToeiRailways.Any(railwayId => stationId.StartsWith(
            $"{railwayId.Replace("odpt.Railway:", "odpt.Station:")}.",
            StringComparison.OrdinalIgnoreCase))
            ? TokyoSubwayOperators[1]
            : null;
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

    private static DateTimeOffset? EarliestFuture(
        IEnumerable<DateTimeOffset?> values,
        DateTimeOffset now) =>
        values
            .Where(value => value.HasValue && value.Value > now)
            .Min();

    private static string GetPublicErrorMessage(Exception exception) => exception switch
    {
        OdptNotConfiguredException => "ODPT 金鑰尚未設定。",
        OdptProviderException { StatusCode: 401 or 403 } => "ODPT 金鑰無效或尚未取得資料權限。",
        OdptProviderException { StatusCode: 429 } => "ODPT 查詢次數已達限制，請稍後再試。",
        OdptProviderException => "ODPT 暫時無法提供東京地鐵資料，請稍後再試。",
        HttpRequestException => "目前無法連線至 ODPT，請稍後再試。",
        _ => "東京地鐵資料暫時無法更新，請稍後再試。"
    };

    private sealed record SubwayOperator(string Id, string CacheKey, string DisplayName);

    private sealed record OperatorFeed<T>(
        SubwayOperator Operator,
        ProviderQueryResult<IReadOnlyList<T>> Result);
}
