namespace TravelInfoAssistant.Api.Providers.HongKong;

public sealed record HongKongGtfsRoute(
    string Id,
    string AgencyId,
    string ShortName,
    string? LongName = null);
public sealed record HongKongGtfsTrip(string Id, string RouteId, string ServiceId, int Direction);
public sealed record HongKongGtfsFrequency(
    string TripId,
    string StartTime,
    string EndTime,
    int HeadwaySeconds);
public sealed record HongKongGtfsStopTime(
    string TripId,
    int Sequence,
    string? DepartureTime,
    string? StopId = null,
    string? ArrivalTime = null);
public sealed record HongKongGtfsStop(
    string Id,
    string Name,
    double? Latitude = null,
    double? Longitude = null);
public sealed record HongKongGtfsCalendar(
    string ServiceId,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DayOfWeek> Days);
public sealed record HongKongGtfsCalendarDate(string ServiceId, DateOnly Date, int ExceptionType);
public sealed record HongKongGtfsSchedule(
    IReadOnlyList<HongKongGtfsRoute> Routes,
    IReadOnlyList<HongKongGtfsTrip> Trips,
    IReadOnlyList<HongKongGtfsFrequency> Frequencies,
    IReadOnlyList<HongKongGtfsStopTime> StopTimes,
    IReadOnlyList<HongKongGtfsCalendar> Calendars,
    IReadOnlyList<HongKongGtfsCalendarDate> CalendarDates,
    IReadOnlyList<HongKongGtfsStop>? Stops = null);
