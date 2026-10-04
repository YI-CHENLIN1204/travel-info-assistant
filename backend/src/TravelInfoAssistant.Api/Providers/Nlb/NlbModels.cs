using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Nlb;

public sealed record NlbHttpResult<T>(T Data, DateTimeOffset FetchedAt);

public sealed class NlbRoutesResponse
{
    [JsonPropertyName("routes")]
    public IReadOnlyList<NlbRouteRow> Routes { get; init; } = [];
}

public sealed class NlbStopsResponse
{
    [JsonPropertyName("stops")]
    public IReadOnlyList<NlbStopRow> Stops { get; init; } = [];
}

public sealed class NlbEtaResponse
{
    [JsonPropertyName("estimatedArrivals")]
    public IReadOnlyList<NlbEtaRow> EstimatedArrivals { get; init; } = [];

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed class NlbRouteRow
{
    [JsonPropertyName("routeId")]
    public string RouteId { get; init; } = string.Empty;

    [JsonPropertyName("routeNo")]
    public string RouteNumber { get; init; } = string.Empty;

    [JsonPropertyName("routeName_c")]
    public string NameZh { get; init; } = string.Empty;

    [JsonPropertyName("routeName_e")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("overnightRoute")]
    public int OvernightRoute { get; init; }

    [JsonPropertyName("specialRoute")]
    public int SpecialRoute { get; init; }
}

public sealed class NlbStopRow
{
    [JsonPropertyName("stopId")]
    public string StopId { get; init; } = string.Empty;

    [JsonPropertyName("stopName_c")]
    public string NameZh { get; init; } = string.Empty;

    [JsonPropertyName("stopName_e")]
    public string NameEn { get; init; } = string.Empty;

    [JsonPropertyName("latitude")]
    public string Latitude { get; init; } = string.Empty;

    [JsonPropertyName("longitude")]
    public string Longitude { get; init; } = string.Empty;
}

public sealed class NlbEtaRow
{
    [JsonPropertyName("estimatedArrivalTime")]
    public string EstimatedArrivalTime { get; init; } = string.Empty;

    [JsonPropertyName("routeVariantName")]
    public string RouteVariantName { get; init; } = string.Empty;

    [JsonPropertyName("departed")]
    public int Departed { get; init; }

    [JsonPropertyName("noGPS")]
    public int GpsEquipped { get; init; }

    [JsonPropertyName("generateTime")]
    public string GenerateTime { get; init; } = string.Empty;
}
