using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public sealed record LtaHttpResult<T>(
    T Data,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceUpdatedAt = null);

public sealed class LtaODataResponse<T>
{
    [JsonPropertyName("value")]
    public IReadOnlyList<T> Value { get; init; } = [];
}

public sealed class LtaDownloadResponse
{
    [JsonPropertyName("value")]
    public IReadOnlyList<LtaDownloadItem> Value { get; init; } = [];
}

public sealed class LtaDownloadItem
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("link")]
    public string Link { get; init; } = string.Empty;
}

public sealed record LtaGtfsRoute(
    string Id,
    string ShortName,
    string LongName,
    string? AgencyId);

public sealed record LtaGtfsStop(
    string Id,
    string Code,
    string Name,
    string? ParentStation,
    int LocationType,
    string? PlatformCode,
    double? Latitude,
    double? Longitude);

public sealed record LtaGtfsTrip(
    string Id,
    string RouteId,
    string? Headsign,
    int DirectionId,
    string? ServiceId = null);

public sealed record LtaGtfsStopTime(
    string TripId,
    string StopId,
    int Sequence,
    string? ArrivalTime,
    string? DepartureTime);

public sealed record LtaGtfsRouteStop(
    string RouteId,
    string StopId,
    int DirectionId,
    int Sequence);

public sealed record LtaGtfsCalendar(
    string ServiceId,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DayOfWeek> Days);

public sealed record LtaGtfsCalendarDate(
    string ServiceId,
    DateOnly Date,
    int ExceptionType);

public sealed record LtaChineseStationName(
    string StationCode,
    string NameEn,
    string NameZh,
    string? LineNameEn,
    string? LineNameZh);

public sealed record LtaGtfsNetwork(
    IReadOnlyList<LtaGtfsRoute> Routes,
    IReadOnlyList<LtaGtfsStop> Stops,
    IReadOnlyList<LtaGtfsTrip> Trips,
    IReadOnlyList<LtaGtfsRouteStop> RouteStops,
    IReadOnlyList<LtaChineseStationName> ChineseNames,
    IReadOnlyList<LtaGtfsStopTime>? StopTimes = null,
    IReadOnlyList<LtaGtfsCalendar>? Calendars = null,
    IReadOnlyList<LtaGtfsCalendarDate>? CalendarDates = null);

public sealed record LtaBusNetwork(
    IReadOnlyList<LtaBusServiceRow> Services,
    IReadOnlyList<LtaBusRouteRow> Routes,
    IReadOnlyList<LtaBusStopRow> Stops);

public sealed class LtaBusServiceRow
{
    public string ServiceNo { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public int Direction { get; init; }
    public string Category { get; init; } = string.Empty;
    public string OriginCode { get; init; } = string.Empty;
    public string DestinationCode { get; init; } = string.Empty;
    public string LoopDesc { get; init; } = string.Empty;
}

public sealed class LtaBusRouteRow
{
    public string ServiceNo { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public int Direction { get; init; }
    public int StopSequence { get; init; }
    public string BusStopCode { get; init; } = string.Empty;
    public double Distance { get; init; }
    public string WD_FirstBus { get; init; } = string.Empty;
    public string WD_LastBus { get; init; } = string.Empty;
    public string SAT_FirstBus { get; init; } = string.Empty;
    public string SAT_LastBus { get; init; } = string.Empty;
    public string SUN_FirstBus { get; init; } = string.Empty;
    public string SUN_LastBus { get; init; } = string.Empty;
}

public sealed class LtaBusStopRow
{
    public string BusStopCode { get; init; } = string.Empty;
    public string RoadName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

public sealed class LtaBusArrivalResponse
{
    public string BusStopCode { get; init; } = string.Empty;
    public IReadOnlyList<LtaBusArrivalService> Services { get; init; } = [];
}

public sealed class LtaBusArrivalService
{
    public string ServiceNo { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public LtaBusArrivalEstimate NextBus { get; init; } = new();
    public LtaBusArrivalEstimate NextBus2 { get; init; } = new();
    public LtaBusArrivalEstimate NextBus3 { get; init; } = new();
}

public sealed class LtaBusArrivalEstimate
{
    public string OriginCode { get; init; } = string.Empty;
    public string DestinationCode { get; init; } = string.Empty;
    public string EstimatedArrival { get; init; } = string.Empty;
    public int Monitored { get; init; }
    public string Load { get; init; } = string.Empty;
    public string Feature { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}

public sealed record LtaRealtimeFeed(
    DateTimeOffset? Timestamp,
    IReadOnlyList<LtaTripUpdate> TripUpdates,
    IReadOnlyList<LtaServiceAlert> Alerts);

public sealed record LtaTripUpdate(
    string Id,
    string? TripId,
    string? RouteId,
    int? DirectionId,
    int ScheduleRelationship,
    DateTimeOffset? Timestamp,
    IReadOnlyList<LtaStopTimeUpdate> StopTimeUpdates);

public sealed record LtaStopTimeUpdate(
    string? StopId,
    int? StopSequence,
    DateTimeOffset? ArrivalTime,
    DateTimeOffset? DepartureTime,
    int? ArrivalDelaySeconds,
    int? DepartureDelaySeconds,
    int ScheduleRelationship);

public sealed record LtaServiceAlert(
    string Id,
    IReadOnlyList<string> RouteIds,
    IReadOnlyList<string> StopIds,
    DateTimeOffset? ActiveFrom,
    DateTimeOffset? ActiveUntil,
    string? Header,
    string? Description,
    int Effect);
