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
