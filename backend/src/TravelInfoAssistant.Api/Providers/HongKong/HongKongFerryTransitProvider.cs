using System.Globalization;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.HongKong;

public interface IHongKongFerryTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetRoutesAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetStopsAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<TransitJourneyScheduleResponse?>> GetJourneysAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken);
}

public sealed class HongKongFerryTransitProvider(
    IHongKongGtfsScheduleProvider scheduleProvider,
    TimeProvider timeProvider) : IHongKongFerryTransitProvider
{
    private const string AgencyId = "FERRY";
    private const string OperatorName = "香港渡輪及街渡";
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
            .Where(IsFerryRoute)
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
                "查無這個方向的渡輪碼頭順序。");
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

    public async Task<ProviderQueryResult<TransitJourneyScheduleResponse?>> GetJourneysAsync(
        string routeId,
        int direction,
        CancellationToken cancellationToken)
    {
        var scheduleResult = await scheduleProvider.GetScheduleAsync(cancellationToken);
        if (scheduleResult.DataStatus == "unavailable")
        {
            return Copy<TransitJourneyScheduleResponse?>(scheduleResult, null);
        }

        var pattern = FindPattern(scheduleResult.Data, routeId, direction);
        if (pattern is null)
        {
            return Copy<TransitJourneyScheduleResponse?>(
                scheduleResult,
                null,
                "查無這個方向的渡輪班表。");
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
        var journeys = BuildJourneys(trips, serviceDate, scheduleResult.Data);
        var first = journeys.FirstOrDefault()?.DepartureAt;
        var last = journeys.LastOrDefault()?.DepartureAt;
        var next = journeys.Where(item => item.DepartureAt >= localNow).Take(10).ToList();
        var stops = scheduleResult.Data.Stops ?? [];
        var originName = StopName(stops, pattern.StopIds.First());
        var destinationName = StopName(stops, pattern.StopIds.Last());
        var data = new TransitJourneyScheduleResponse(
            routeId,
            direction,
            pattern.StopIds.First(),
            originName,
            destinationName,
            first,
            last,
            next);

        var serviceDayStatus = journeys.Count == 0
            ? "no-service"
            : localNow < first
                ? "not-started"
                : localNow > last
                    ? "ended"
                    : "active";
        var message = serviceDayStatus switch
        {
            "no-service" => "本日沒有表定船班。",
            "not-started" => $"今日首班船將於 {first:HH:mm} 從起點碼頭開出。",
            "ended" => $"本日已無船班，末班船已於 {last:HH:mm} 駛離碼頭。",
            _ => "官方表定船班，非即時航行資訊。"
        };

        return new ProviderQueryResult<TransitJourneyScheduleResponse?>(
            data,
            scheduleResult.DataStatus,
            scheduleResult.SourceUpdatedAt,
            scheduleResult.FetchedAt,
            scheduleResult.Stale,
            message,
            scheduleResult.Source,
            serviceDayStatus,
            last,
            last.HasValue ? $"末班船已於 {last:HH:mm} 駛離碼頭" : null);
    }

    private static TransitRouteResponse MapRoute(
        HongKongGtfsRoute route,
        HongKongGtfsSchedule schedule)
    {
        var patterns = new[] { 0, 1 }
            .Select(direction => FindPattern(schedule, route.Id, direction))
            .OfType<FerryPattern>()
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

    private static FerryPattern? FindPattern(
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
            .Select(group => new FerryPattern(direction, group.First()))
            .FirstOrDefault();
    }

    private static List<TransitScheduledJourneyResponse> BuildJourneys(
        IReadOnlyCollection<HongKongGtfsTrip> trips,
        DateOnly serviceDate,
        HongKongGtfsSchedule schedule)
    {
        var journeys = new List<TransitScheduledJourneyResponse>();
        foreach (var trip in trips)
        {
            var stopTimes = schedule.StopTimes
                .Where(item => item.TripId.Equals(trip.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(item => item.Sequence)
                .ToList();
            if (stopTimes.Count < 2) continue;

            var departure = ParseServiceTime(
                stopTimes.First().DepartureTime ?? stopTimes.First().ArrivalTime,
                serviceDate);
            var arrival = ParseServiceTime(
                stopTimes.Last().ArrivalTime ?? stopTimes.Last().DepartureTime,
                serviceDate);
            if (!departure.HasValue) continue;

            var frequencies = schedule.Frequencies
                .Where(item => item.TripId.Equals(trip.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (frequencies.Count == 0)
            {
                journeys.Add(new TransitScheduledJourneyResponse(departure.Value, arrival));
                continue;
            }

            var duration = arrival.HasValue ? arrival.Value - departure.Value : (TimeSpan?)null;
            foreach (var frequency in frequencies)
            {
                var start = ParseServiceTime(frequency.StartTime, serviceDate);
                var end = ParseServiceTime(frequency.EndTime, serviceDate);
                if (!start.HasValue || !end.HasValue || frequency.HeadwaySeconds <= 0) continue;
                for (var value = start.Value; value < end.Value; value = value.AddSeconds(frequency.HeadwaySeconds))
                {
                    journeys.Add(new TransitScheduledJourneyResponse(
                        value,
                        duration.HasValue ? value.Add(duration.Value) : null));
                }
            }
        }

        return journeys
            .GroupBy(item => item.DepartureAt)
            .Select(group => group.OrderByDescending(item => item.ArrivalAt.HasValue).First())
            .OrderBy(item => item.DepartureAt)
            .ToList();
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

    private static bool IsFerryRoute(HongKongGtfsRoute route) =>
        route.AgencyId.Equals(AgencyId, StringComparison.OrdinalIgnoreCase);

    private static int ParseRouteNumber(string routeId) =>
        int.TryParse(routeId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;

    private static string CleanRouteName(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var result = value.Trim();
        foreach (var marker in new[] { "(單程成人收費", "(持有樂悠卡" })
        {
            var suffix = result.IndexOf(marker, StringComparison.Ordinal);
            if (suffix >= 0) result = result[..suffix].Trim();
        }
        return result;
    }

    private static string StopName(IReadOnlyList<HongKongGtfsStop> stops, string stopId) =>
        CleanStopName(stops.FirstOrDefault(item =>
            item.Id.Equals(stopId, StringComparison.OrdinalIgnoreCase))?.Name ?? stopId);

    private static string CleanStopName(string value)
    {
        var result = value.Trim();
        if (result.StartsWith("[FERRY]", StringComparison.OrdinalIgnoreCase))
        {
            result = result[7..].Trim();
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

    private sealed record FerryPattern(int Direction, IReadOnlyList<string> StopIds);
}
