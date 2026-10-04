using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Kmb;

public sealed record KmbHttpResult<T>(
    T Data,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceUpdatedAt = null);

public sealed class KmbResponse<T>
{
    [JsonPropertyName("generated_timestamp")]
    public DateTimeOffset? GeneratedAt { get; init; }

    [JsonPropertyName("data")]
    public T Data { get; init; } = default!;
}

public sealed class KmbRouteRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("bound")]
    public string Bound { get; init; } = string.Empty;

    [JsonPropertyName("service_type")]
    public string ServiceType { get; init; } = string.Empty;

    [JsonPropertyName("orig_tc")]
    public string OriginZh { get; init; } = string.Empty;

    [JsonPropertyName("orig_en")]
    public string OriginEn { get; init; } = string.Empty;

    [JsonPropertyName("dest_tc")]
    public string DestinationZh { get; init; } = string.Empty;

    [JsonPropertyName("dest_en")]
    public string DestinationEn { get; init; } = string.Empty;
}

public sealed class KmbRouteStopRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("bound")]
    public string Bound { get; init; } = string.Empty;

    [JsonPropertyName("service_type")]
    public string ServiceType { get; init; } = string.Empty;

    [JsonPropertyName("seq")]
    public string Sequence { get; init; } = string.Empty;

    [JsonPropertyName("stop")]
    public string StopId { get; init; } = string.Empty;
}

public sealed class KmbStopRow
{
    [JsonPropertyName("stop")]
    public string StopId { get; init; } = string.Empty;

    [JsonPropertyName("name_tc")]
    public string NameZh { get; init; } = string.Empty;

    [JsonPropertyName("name_en")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("lat")]
    public string Latitude { get; init; } = string.Empty;

    [JsonPropertyName("long")]
    public string Longitude { get; init; } = string.Empty;
}

public sealed class KmbEtaRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("dir")]
    public string Direction { get; init; } = string.Empty;

    [JsonPropertyName("service_type")]
    public int ServiceType { get; init; }

    [JsonPropertyName("seq")]
    public int Sequence { get; init; }

    [JsonPropertyName("eta_seq")]
    public int EtaSequence { get; init; }

    [JsonPropertyName("dest_tc")]
    public string DestinationZh { get; init; } = string.Empty;

    [JsonPropertyName("eta")]
    public DateTimeOffset? EstimatedAt { get; init; }

    [JsonPropertyName("rmk_tc")]
    public string RemarkZh { get; init; } = string.Empty;

    [JsonPropertyName("data_timestamp")]
    public DateTimeOffset? DataTimestamp { get; init; }
}

public sealed record KmbGtfsRoute(string Id, string AgencyId, string ShortName);
public sealed record KmbGtfsTrip(string Id, string RouteId, string ServiceId, int Direction);
public sealed record KmbGtfsFrequency(
    string TripId,
    string StartTime,
    string EndTime,
    int HeadwaySeconds);
public sealed record KmbGtfsStopTime(string TripId, int Sequence, string? DepartureTime);
public sealed record KmbGtfsCalendar(
    string ServiceId,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<DayOfWeek> Days);
public sealed record KmbGtfsCalendarDate(string ServiceId, DateOnly Date, int ExceptionType);
public sealed record KmbGtfsSchedule(
    IReadOnlyList<KmbGtfsRoute> Routes,
    IReadOnlyList<KmbGtfsTrip> Trips,
    IReadOnlyList<KmbGtfsFrequency> Frequencies,
    IReadOnlyList<KmbGtfsStopTime> StopTimes,
    IReadOnlyList<KmbGtfsCalendar> Calendars,
    IReadOnlyList<KmbGtfsCalendarDate> CalendarDates);
