using System.Globalization;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.HongKong;

public interface IHongKongTramTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetRoutesAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetStopsAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<TransitDepartureScheduleResponse?>> GetDeparturesAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken);
}

public sealed class HongKongTramTransitProvider(
    IHongKongGtfsScheduleProvider scheduleProvider,
    TimeProvider timeProvider) : IHongKongTramTransitProvider
{
    private const string AgencyId = "TRAM";
    private const string OperatorName = "香港電車";
    private static readonly TimeZoneInfo HongKongTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong");
    private static readonly TimeOnly ServiceDayBoundary = new(4, 0);

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetRoutesAsync(
        CancellationToken cancellationToken)
    {
        var scheduleResult = await scheduleProvider.GetScheduleAsync(cancellationToken);
        if (scheduleResult.DataStatus == "unavailable")
        {
            return Copy(
                scheduleResult,
                (IReadOnlyList<TransitRouteResponse>)Array.Empty<TransitRouteResponse>());
        }

        IReadOnlyList<TransitRouteResponse> routes = scheduleResult.Data.Routes
            .Where(IsTramRoute)
            .OrderBy(item => ParseRouteNumber(item.Id))
            .Select(route => MapRoute(route, scheduleResult.Data))
            .Where(item => item.Directions.Count > 0)
            .ToList();
        return Copy(scheduleResult, routes);
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetStopsAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await scheduleProvider.GetScheduleAsync(cancellationToken);
        if (scheduleResult.DataStatus == "unavailable")
        {
            return Copy(
                scheduleResult,
                (IReadOnlyList<TransitStopResponse>)Array.Empty<TransitStopResponse>());
        }

        var pattern = FindPattern(scheduleResult.Data, routeId, direction);
        if (pattern is null)
        {
            return Copy(
                scheduleResult,
                (IReadOnlyList<TransitStopResponse>)Array.Empty<TransitStopResponse>(),
                "查無這個方向的香港電車站序。");
        }

        var stopsById = (scheduleResult.Data.Stops ?? [])
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<TransitStopResponse> stops = pattern.StopIds
            .Select((stopId, index) => stopsById.TryGetValue(stopId, out var stop)
                ? new TransitStopResponse(
                    stop.Id,
                    CleanStopName(stop.Name),
                    null,
                    index + 1,
                    direction,
                    stop.Latitude,
                    stop.Longitude)
                : null)
            .OfType<TransitStopResponse>()
            .ToList();
        return Copy(scheduleResult, stops);
    }

    public async Task<ProviderQueryResult<TransitDepartureScheduleResponse?>> GetDeparturesAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await scheduleProvider.GetScheduleAsync(cancellationToken);
        if (scheduleResult.DataStatus == "unavailable")
        {
            return Copy<TransitDepartureScheduleResponse?>(scheduleResult, null);
        }

        var pattern = FindPattern(scheduleResult.Data, routeId, direction);
        if (pattern is null)
        {
            return Copy<TransitDepartureScheduleResponse?>(
                scheduleResult,
                null,
                "查無這個方向的香港電車班表。");
        }

        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), HongKongTimeZone);
        var serviceDate = DateOnly.FromDateTime(localNow.DateTime);
        if (TimeOnly.FromDateTime(localNow.DateTime) < ServiceDayBoundary)
        {
            serviceDate = serviceDate.AddDays(-1);
        }

        var trips = scheduleResult.Data.Trips
            .Where(item =>
                item.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase) &&
                DirectionFor(item, routeId) == direction &&
                IsServiceActive(item.ServiceId, serviceDate, scheduleResult.Data))
            .ToList();
        var departures = BuildDepartures(trips, serviceDate, scheduleResult.Data);
        var first = departures.FirstOrDefault();
        var last = departures.LastOrDefault();
        var next = departures.Where(item => item >= localNow).Take(10).ToList();
        var stops = scheduleResult.Data.Stops ?? [];
        var originName = StopName(stops, pattern.StopIds.First());
        var destinationName = StopName(stops, pattern.StopIds.Last());
        var data = new TransitDepartureScheduleResponse(
            routeId,
            direction,
            pattern.StopIds.First(),
            originName,
            destinationName,
            first == default ? null : first,
            last == default ? null : last,
            next);

        var serviceDayStatus = departures.Count == 0
            ? "no-service"
            : localNow < first
                ? "not-started"
                : localNow > last
                    ? "ended"
                    : "active";
        var message = serviceDayStatus switch
        {
            "no-service" => "本日沒有表定電車班次。",
            "not-started" => $"今日首班車將於 {first:HH:mm} 從起點站開出。",
            "ended" => $"本日已無車次，末班車已於 {last:HH:mm} 駛離起點站。",
            _ => "起點班表，非本站即時到站時間。"
        };

        return new ProviderQueryResult<TransitDepartureScheduleResponse?>(
            data,
            scheduleResult.DataStatus,
            scheduleResult.SourceUpdatedAt,
            scheduleResult.FetchedAt,
            scheduleResult.Stale,
            message,
            scheduleResult.Source,
            serviceDayStatus,
            last == default ? null : last,
            last == default ? null : $"末班車已於 {last:HH:mm} 駛離起點站");
    }

    private static TransitRouteResponse MapRoute(
        HongKongGtfsRoute route,
        HongKongGtfsSchedule schedule)
    {
        var patterns = new[] { 0, 1 }
            .Select(direction => FindPattern(schedule, route.Id, direction))
            .OfType<TramPattern>()
            .ToList();
        var stops = schedule.Stops ?? [];
        var directions = patterns.Select(pattern => new TransitDirectionResponse(
            pattern.Direction,
            StopName(stops, pattern.StopIds.Last()),
            StopName(stops, pattern.StopIds.First()),
            StopName(stops, pattern.StopIds.Last()))).ToList();
        var primary = patterns.FirstOrDefault();
        return new TransitRouteResponse(
            route.Id,
            CleanRouteName(route.LongName, route.Id),
            null,
            primary is null ? null : StopName(stops, primary.StopIds.First()),
            primary is null ? null : StopName(stops, primary.StopIds.Last()),
            [OperatorName],
            directions,
            primary?.StopIds.Select(stopId => StopName(stops, stopId)).ToList() ?? [],
            route.Id);
    }

    private static TramPattern? FindPattern(
        HongKongGtfsSchedule schedule,
        string routeId,
        int direction)
    {
        var tripIds = schedule.Trips
            .Where(item =>
                item.RouteId.Equals(routeId, StringComparison.OrdinalIgnoreCase) &&
                DirectionFor(item, routeId) == direction)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return schedule.StopTimes
            .Where(item => tripIds.Contains(item.TripId) && !string.IsNullOrWhiteSpace(item.StopId))
            .GroupBy(item => item.TripId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(item => item.Sequence).Select(item => item.StopId!).ToList())
            .Where(item => item.Count >= 2)
            .GroupBy(item => string.Join('|', item), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.First().Count)
            .Select(group => new TramPattern(direction, group.First()))
            .FirstOrDefault();
    }

    private static List<DateTimeOffset> BuildDepartures(
        IReadOnlyCollection<HongKongGtfsTrip> trips,
        DateOnly serviceDate,
        HongKongGtfsSchedule schedule)
    {
        var tripIds = trips.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = new List<DateTimeOffset>();
        foreach (var frequency in schedule.Frequencies.Where(item => tripIds.Contains(item.TripId)))
        {
            var start = ParseServiceTime(frequency.StartTime, serviceDate);
            var end = ParseServiceTime(frequency.EndTime, serviceDate);
            if (!start.HasValue || !end.HasValue || frequency.HeadwaySeconds <= 0) continue;
            for (var departure = start.Value; departure < end.Value; departure = departure.AddSeconds(frequency.HeadwaySeconds))
            {
                values.Add(departure);
            }
        }

        foreach (var group in schedule.StopTimes
                     .Where(item => tripIds.Contains(item.TripId))
                     .GroupBy(item => item.TripId, StringComparer.OrdinalIgnoreCase))
        {
            var originTime = group.OrderBy(item => item.Sequence).FirstOrDefault()?.DepartureTime;
            var departure = ParseServiceTime(originTime, serviceDate);
            if (departure.HasValue) values.Add(departure.Value);
        }

        return values.Distinct().Order().ToList();
    }

    private static bool IsServiceActive(
        string serviceId,
        DateOnly date,
        HongKongGtfsSchedule schedule)
    {
        var exception = schedule.CalendarDates.LastOrDefault(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) && item.Date == date);
        if (exception is not null) return exception.ExceptionType == 1;
        return schedule.Calendars.Any(item =>
            item.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase) &&
            date >= item.StartDate && date <= item.EndDate && item.Days.Contains(date.DayOfWeek));
    }

    private static DateTimeOffset? ParseServiceTime(string? value, DateOnly serviceDate)
    {
        var parts = value?.Split(':');
        if (parts is not { Length: >= 2 } ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }
        var local = serviceDate.ToDateTime(TimeOnly.MinValue).AddHours(hours).AddMinutes(minutes);
        return new DateTimeOffset(local, HongKongTimeZone.GetUtcOffset(local));
    }

    private static int DirectionFor(HongKongGtfsTrip trip, string routeId)
    {
        var prefix = routeId + "-";
        if (trip.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var marker = trip.Id[prefix.Length..].Split('-', 2)[0];
            if (marker == "1") return 0;
            if (marker == "2") return 1;
        }
        return trip.Direction;
    }

    private static bool IsTramRoute(HongKongGtfsRoute route) =>
        route.AgencyId.Equals(AgencyId, StringComparison.OrdinalIgnoreCase);

    private static int ParseRouteNumber(string routeId) =>
        int.TryParse(routeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;

    private static string CleanRouteName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var suffix = value.IndexOf("(持有樂悠卡", StringComparison.Ordinal);
        return (suffix >= 0 ? value[..suffix] : value).Trim();
    }

    private static string StopName(IReadOnlyList<HongKongGtfsStop> stops, string stopId) =>
        CleanStopName(stops.FirstOrDefault(item =>
            item.Id.Equals(stopId, StringComparison.OrdinalIgnoreCase))?.Name ?? stopId);

    private static string CleanStopName(string value)
    {
        var result = value.Trim();
        if (result.StartsWith("[TRAM]", StringComparison.OrdinalIgnoreCase))
        {
            result = result[6..].Trim();
        }
        return result;
    }

    private static ProviderQueryResult<TTarget> Copy<TTarget>(
        ProviderQueryResult<HongKongGtfsSchedule> source,
        TTarget data,
        string? message = null) =>
        new(
            data,
            source.DataStatus,
            source.SourceUpdatedAt,
            source.FetchedAt,
            source.Stale,
            message ?? source.Message,
            source.Source);

    private sealed record TramPattern(int Direction, IReadOnlyList<string> StopIds);
}
