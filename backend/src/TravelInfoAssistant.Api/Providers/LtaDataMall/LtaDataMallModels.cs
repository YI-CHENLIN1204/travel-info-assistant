using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public sealed record LtaHttpResult<T>(
    T Data,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceUpdatedAt = null);

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
    int DirectionId);

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
    IReadOnlyList<LtaChineseStationName> ChineseNames);

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
