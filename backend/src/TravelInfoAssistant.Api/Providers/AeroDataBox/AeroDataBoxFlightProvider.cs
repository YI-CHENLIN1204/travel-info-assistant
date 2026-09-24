using System.Globalization;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Flights;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public sealed class AeroDataBoxFlightProvider(
    IAeroDataBoxApiClient apiClient,
    IProviderCache cache,
    IAirportCatalog airportCatalog,
    TimeProvider timeProvider,
    ILogger<AeroDataBoxFlightProvider> logger) : IAeroDataBoxFlightProvider
{
    private static readonly TimeSpan LiveFreshFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ScheduledFreshFor = TimeSpan.FromHours(6);
    private static readonly TimeSpan HistoricalFreshFor = TimeSpan.FromDays(1);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(7);
    private static readonly TimeSpan StatusLiveFreshFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StatusScheduledFreshFor = TimeSpan.FromHours(6);
    private static readonly TimeSpan StatusRetainFor = TimeSpan.FromDays(1);

    public async Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> SearchRouteAsync(
        string originIata,
        string destinationIata,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var origin = airportCatalog.FindByIata(originIata);
        if (origin is null)
        {
            return Unavailable("找不到出發機場，請重新選擇機場。 ");
        }

        var first = await GetFidsBlockAsync(origin, date, 0, cancellationToken);
        var second = await GetFidsBlockAsync(origin, date, 1, cancellationToken);
        var usable = new[] { first, second }
            .Where(item => item.DataStatus != "unavailable")
            .ToList();
        if (usable.Count == 0)
        {
            return Unavailable(first.Message ?? second.Message ?? "航班資料暫時無法更新。");
        }

        var segments = usable
            .SelectMany(item => item.Data)
            .Where(item => string.Equals(
                item.Arrival.Airport.Iata,
                destinationIata,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Departure.Scheduled?.Utc)
            .ThenBy(item => item.FlightNumber)
            .ToList();
        var itineraries = segments
            .Select(segment => new FlightItineraryResponse(
                $"itinerary:{segment.Id}",
                0,
                [segment]))
            .ToList();

        var messages = new[] { first.Message, second.Message }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (usable.Count < 2)
        {
            messages.Add("目前僅取得部分時段的航班資料。");
        }

        return new ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>(
            itineraries,
            CombineDataStatus(usable),
            Latest(usable.Select(item => item.SourceUpdatedAt)),
            usable.Max(item => item.FetchedAt),
            usable.Any(item => item.Stale),
            messages.Count == 0 ? null : string.Join(' ', messages));
    }

    public async Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> GetFlightAsync(
        string flightNumber,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var normalizedNumber = new string(flightNumber
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        var key = $"flight:aerodatabox:status:{normalizedNumber}:{date:yyyy-MM-dd}:v1";
        var freshFor = IsNearToday(date) ? StatusLiveFreshFor : StatusScheduledFreshFor;

        return await GetCachedSafelyAsync(
            key,
            freshFor,
            StatusRetainFor,
            async token =>
            {
                var path = $"flights/number/{Uri.EscapeDataString(normalizedNumber)}/{date:yyyy-MM-dd}";
                var response = await apiClient.GetAsync<IReadOnlyList<AeroDataBoxFlight>>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["dateLocalRole"] = "Both",
                        ["withAircraftImage"] = "false",
                        ["withLocation"] = "false",
                        ["withFlightPlan"] = "false"
                    },
                    AeroDataBoxRequestKind.FlightStatus,
                    token);
                var source = response.Data ?? [];
                var flights = source
                    .Select(AeroDataBoxFlightMapper.MapFlight)
                    .Where(item => item is not null)
                    .Cast<FlightSegmentResponse>()
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(item => item.Departure.Scheduled?.Utc)
                    .Select(segment => new FlightItineraryResponse(
                        $"itinerary:{segment.Id}",
                        0,
                        [segment]))
                    .ToList();

                return new ProviderPayload<IReadOnlyList<FlightItineraryResponse>>(
                    flights,
                    source.Any(AeroDataBoxFlightMapper.HasLiveData) ? "realtime" : "scheduled",
                    Latest(source.Select(AeroDataBoxFlightMapper.GetLastUpdatedUtc)) ??
                    response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);
    }

    private Task<ProviderQueryResult<IReadOnlyList<FlightSegmentResponse>>> GetFidsBlockAsync(
        AirportResponse origin,
        DateOnly date,
        int block,
        CancellationToken cancellationToken)
    {
        var (from, to) = block == 0 ? ("00:00", "11:59") : ("12:00", "23:59");
        var key = $"flight:aerodatabox:fids:{origin.Iata.ToLowerInvariant()}:{date:yyyy-MM-dd}:{block}:v1";
        var freshFor = GetFidsFreshFor(date);

        return GetCachedSafelyAsync(
            key,
            freshFor,
            RetainFor,
            async token =>
            {
                var dateText = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var path = "flights/airports/iata/" +
                           $"{Uri.EscapeDataString(origin.Iata)}/" +
                           $"{dateText}T{from}/{dateText}T{to}";
                var response = await apiClient.GetAsync<AeroDataBoxFidsResponse>(
                    path,
                    new Dictionary<string, string?>
                    {
                        ["direction"] = "Departure",
                        ["withLeg"] = "true",
                        ["withCancelled"] = "true",
                        ["withCodeshared"] = "false",
                        ["withCargo"] = "false",
                        ["withPrivate"] = "false",
                        ["withLocation"] = "false"
                    },
                    AeroDataBoxRequestKind.RouteSearch,
                    token);
                var source = response.Data?.Departures ?? [];
                var flights = source
                    .Select(item => AeroDataBoxFlightMapper.MapFidsFlight(item, origin))
                    .Where(item => item is not null)
                    .Cast<FlightSegmentResponse>()
                    .ToList();

                return new ProviderPayload<IReadOnlyList<FlightSegmentResponse>>(
                    flights,
                    source.Any(AeroDataBoxFlightMapper.HasLiveData) ? "realtime" : "scheduled",
                    response.LastModified,
                    response.FetchedAt);
            },
            cancellationToken);
    }

    private async Task<ProviderQueryResult<T>> GetCachedSafelyAsync<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<ProviderPayload<T>>> factory,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await cache.GetOrCreateAsync(
                key,
                freshFor,
                retainFor,
                factory,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "AeroDataBox query failed for {CacheKey}.", key);
            return ProviderQueryResult<T>.Unavailable(
                EmptyValue<T>(),
                GetPublicErrorMessage(exception),
                timeProvider);
        }
    }

    private TimeSpan GetFidsFreshFor(DateOnly date)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (Math.Abs(date.DayNumber - today.DayNumber) <= 1)
        {
            return LiveFreshFor;
        }

        return date < today ? HistoricalFreshFor : ScheduledFreshFor;
    }

    private bool IsNearToday(DateOnly date)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return Math.Abs(date.DayNumber - today.DayNumber) <= 1;
    }

    private ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>> Unavailable(string message) =>
        ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>.Unavailable(
            Array.Empty<FlightItineraryResponse>(),
            message,
            timeProvider);

    private static T EmptyValue<T>() where T : class
    {
        if (typeof(T) == typeof(IReadOnlyList<FlightSegmentResponse>))
        {
            return (T)(object)Array.Empty<FlightSegmentResponse>();
        }

        if (typeof(T) == typeof(IReadOnlyList<FlightItineraryResponse>))
        {
            return (T)(object)Array.Empty<FlightItineraryResponse>();
        }

        throw new InvalidOperationException($"No empty value is configured for {typeof(T).Name}.");
    }

    private static string CombineDataStatus(
        IReadOnlyList<ProviderQueryResult<IReadOnlyList<FlightSegmentResponse>>> results)
    {
        if (results.Any(item => item.DataStatus == "realtime")) return "realtime";
        if (results.Any(item => item.DataStatus == "cached")) return "cached";
        return "scheduled";
    }

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values)
    {
        var latest = values
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .DefaultIfEmpty()
            .Max();
        return latest == default ? null : latest;
    }

    private static string GetPublicErrorMessage(Exception exception) => exception switch
    {
        AeroDataBoxNotConfiguredException =>
            "AeroDataBox 金鑰尚未設定；機場搜尋仍可使用，但目前無法取得真實航班資料。",
        AeroDataBoxQuotaExceededException =>
            "本期 AeroDataBox 額度已達內部停止線，暫停新的外部查詢以避免產生費用。",
        AeroDataBoxProviderException => "AeroDataBox 暫時無法提供資料，請稍後再試。",
        HttpRequestException => "目前無法連線至 AeroDataBox，請稍後再試。",
        _ => "航班資料暫時無法更新，請稍後再試。"
    };
}
