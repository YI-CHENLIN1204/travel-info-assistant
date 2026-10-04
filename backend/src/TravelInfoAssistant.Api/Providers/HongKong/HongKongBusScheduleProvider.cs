using System.Globalization;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.HongKong;

public interface IHongKongBusScheduleProvider
{
    Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(
        CancellationToken cancellationToken);

    DateTimeOffset? FindLastOriginDeparture(
        string agencyId,
        string route,
        int direction,
        DateTimeOffset now,
        HongKongGtfsSchedule schedule,
        string? originName = null);
}

public sealed class HongKongBusScheduleProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<HongKongTransitOptions> options,
    IProviderCache cache,
    TimeProvider timeProvider) : IHongKongBusScheduleProvider
{
    private static readonly TimeZoneInfo HongKongTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong");
    private static readonly TimeOnly ServiceDayBoundary = new(4, 0);
    private static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(7);

    public async Task<ProviderQueryResult<HongKongGtfsSchedule>> GetScheduleAsync(
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            "transit:hong-kong:bus-schedule:v2",
            FreshFor,
            RetainFor,
            async token =>
            {
                var client = httpClientFactory.CreateClient("hong-kong-gtfs");
                using var response = await client.GetAsync(options.Value.ScheduleUrl, token);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(token);
                return new ProviderPayload<HongKongGtfsSchedule>(
                    HongKongGtfsParser.Parse(bytes),
                    "scheduled",
                    response.Content.Headers.LastModified,
                    timeProvider.GetUtcNow());
            },
            cancellationToken);
        return result with { Source = "香港運輸署 GTFS" };
    }

    public DateTimeOffset? FindLastOriginDeparture(
        string agencyId,
        string route,
        int direction,
        DateTimeOffset now,
        HongKongGtfsSchedule schedule,
        string? originName = null)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, HongKongTimeZone);
        var serviceDate = DateOnly.FromDateTime(localNow.DateTime);
        if (TimeOnly.FromDateTime(localNow.DateTime) < ServiceDayBoundary)
        {
            serviceDate = serviceDate.AddDays(-1);
        }
        var routeIds = schedule.Routes
            .Where(item =>
                item.AgencyId.Equals(agencyId, StringComparison.OrdinalIgnoreCase) &&
                item.ShortName.Equals(route, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var trips = schedule.Trips
            .Where(item =>
                routeIds.Contains(item.RouteId) &&
                IsRequestedDirection(item, direction, originName, schedule) &&
                IsServiceActive(item.ServiceId, serviceDate, schedule))
            .ToList();
        if (trips.Count == 0) return null;

        var tripIds = trips.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = schedule.Frequencies
            .Where(item => tripIds.Contains(item.TripId))
            .Select(item => LastFrequencyDeparture(item, serviceDate))
            .Concat(schedule.StopTimes
                .Where(item => tripIds.Contains(item.TripId))
                .GroupBy(item => item.TripId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(item => item.Sequence).First().DepartureTime)
                .Select(item => ParseServiceTime(item, serviceDate)))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        return candidates.Count == 0 ? null : candidates.Max();
    }

    private static bool IsRequestedDirection(
        HongKongGtfsTrip trip,
        int direction,
        string? originName,
        HongKongGtfsSchedule schedule)
    {
        if (string.IsNullOrWhiteSpace(originName)) return trip.Direction == direction;

        var firstStopId = schedule.StopTimes
            .Where(item => item.TripId.Equals(trip.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Sequence)
            .Select(item => item.StopId)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstStopId)) return false;

        var firstStopName = schedule.Stops?.FirstOrDefault(item =>
            item.Id.Equals(firstStopId, StringComparison.OrdinalIgnoreCase))?.Name;
        return NormalizeStopName(firstStopName).Equals(
            NormalizeStopName(originName),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStopName(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.StartsWith("[", StringComparison.Ordinal) && normalized.IndexOf(']') is var end && end >= 0)
        {
            normalized = normalized[(end + 1)..].Trim();
        }
        return normalized;
    }

    private static DateTimeOffset? LastFrequencyDeparture(
        HongKongGtfsFrequency frequency,
        DateOnly serviceDate)
    {
        var start = ParseServiceTime(frequency.StartTime, serviceDate);
        var end = ParseServiceTime(frequency.EndTime, serviceDate);
        if (!start.HasValue || !end.HasValue || frequency.HeadwaySeconds <= 0 || end <= start)
        {
            return null;
        }

        var serviceSeconds = (end.Value - start.Value).TotalSeconds;
        var intervals = Math.Max(0, (int)Math.Ceiling(serviceSeconds / frequency.HeadwaySeconds) - 1);
        return start.Value.AddSeconds(intervals * frequency.HeadwaySeconds);
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
}
