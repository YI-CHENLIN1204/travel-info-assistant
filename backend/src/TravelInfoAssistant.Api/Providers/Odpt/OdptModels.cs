using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public sealed class OdptLocalizedTitle
{
    [JsonPropertyName("ja")]
    public string? Ja { get; init; }

    [JsonPropertyName("en")]
    public string? En { get; init; }
}

public sealed class OdptStationOrder
{
    [JsonPropertyName("odpt:index")]
    public int Index { get; init; }

    [JsonPropertyName("odpt:station")]
    public string? Station { get; init; }

    [JsonPropertyName("odpt:stationTitle")]
    public OdptLocalizedTitle? StationTitle { get; init; }
}

public sealed class OdptRailway
{
    [JsonPropertyName("owl:sameAs")]
    public string? SameAs { get; init; }

    [JsonPropertyName("dc:title")]
    public string? Title { get; init; }

    [JsonPropertyName("dc:date")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("odpt:railwayTitle")]
    public OdptLocalizedTitle? RailwayTitle { get; init; }

    [JsonPropertyName("odpt:operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("odpt:stationOrder")]
    public IReadOnlyList<OdptStationOrder> StationOrder { get; init; } = [];
}

public sealed class OdptStation
{
    [JsonPropertyName("owl:sameAs")]
    public string? SameAs { get; init; }

    [JsonPropertyName("dc:title")]
    public string? Title { get; init; }

    [JsonPropertyName("dc:date")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("odpt:stationTitle")]
    public OdptLocalizedTitle? StationTitle { get; init; }

    [JsonPropertyName("odpt:stationCode")]
    public string? StationCode { get; init; }

    [JsonPropertyName("odpt:operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("odpt:railway")]
    public string? Railway { get; init; }

    [JsonPropertyName("geo:lat")]
    public double? Latitude { get; init; }

    [JsonPropertyName("geo:long")]
    public double? Longitude { get; init; }
}

public sealed class OdptCalendar
{
    [JsonPropertyName("owl:sameAs")]
    public string? SameAs { get; init; }

    [JsonPropertyName("dc:date")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("odpt:day")]
    public IReadOnlyList<DateOnly> Days { get; init; } = [];

    [JsonPropertyName("odpt:duration")]
    public string? Duration { get; init; }
}

public sealed class OdptStationTimetable
{
    [JsonPropertyName("owl:sameAs")]
    public string? SameAs { get; init; }

    [JsonPropertyName("dc:date")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("odpt:railway")]
    public string? Railway { get; init; }

    [JsonPropertyName("odpt:railwayTitle")]
    public OdptLocalizedTitle? RailwayTitle { get; init; }

    [JsonPropertyName("odpt:station")]
    public string? Station { get; init; }

    [JsonPropertyName("odpt:stationTitle")]
    public OdptLocalizedTitle? StationTitle { get; init; }

    [JsonPropertyName("odpt:railDirection")]
    public string? RailDirection { get; init; }

    [JsonPropertyName("odpt:calendar")]
    public string? Calendar { get; init; }

    [JsonPropertyName("odpt:stationTimetableObject")]
    public IReadOnlyList<OdptStationTimetableObject> Objects { get; init; } = [];
}

public sealed class OdptStationTimetableObject
{
    [JsonPropertyName("odpt:arrivalTime")]
    public string? ArrivalTime { get; init; }

    [JsonPropertyName("odpt:departureTime")]
    public string? DepartureTime { get; init; }

    [JsonPropertyName("odpt:destinationStation")]
    public IReadOnlyList<string> DestinationStations { get; init; } = [];

    [JsonPropertyName("odpt:train")]
    public string? Train { get; init; }

    [JsonPropertyName("odpt:trainNumber")]
    public string? TrainNumber { get; init; }

    [JsonPropertyName("odpt:trainType")]
    public string? TrainType { get; init; }

    [JsonPropertyName("odpt:isLast")]
    public bool IsLast { get; init; }

    [JsonPropertyName("odpt:platformNumber")]
    public string? PlatformNumber { get; init; }

    [JsonPropertyName("odpt:platformName")]
    public OdptLocalizedTitle? PlatformName { get; init; }
}
