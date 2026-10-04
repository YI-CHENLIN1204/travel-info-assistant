using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Citybus;

public sealed record CitybusHttpResult<T>(
    T Data,
    DateTimeOffset FetchedAt,
    DateTimeOffset? SourceUpdatedAt = null);

public sealed class CitybusResponse<T>
{
    [JsonPropertyName("data")]
    public T Data { get; init; } = default!;
}

public sealed class CitybusRouteRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("orig_tc")]
    public string OriginZh { get; init; } = string.Empty;

    [JsonPropertyName("orig_en")]
    public string OriginEn { get; init; } = string.Empty;

    [JsonPropertyName("dest_tc")]
    public string DestinationZh { get; init; } = string.Empty;

    [JsonPropertyName("dest_en")]
    public string DestinationEn { get; init; } = string.Empty;

    [JsonPropertyName("data_timestamp")]
    public DateTimeOffset? DataTimestamp { get; init; }
}

public sealed class CitybusRouteStopRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("dir")]
    public string Direction { get; init; } = string.Empty;

    [JsonPropertyName("seq")]
    public int Sequence { get; init; }

    [JsonPropertyName("stop")]
    public string StopId { get; init; } = string.Empty;

    [JsonPropertyName("data_timestamp")]
    public DateTimeOffset? DataTimestamp { get; init; }
}

public sealed class CitybusStopRow
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

    [JsonPropertyName("data_timestamp")]
    public DateTimeOffset? DataTimestamp { get; init; }
}

public sealed class CitybusEtaRow
{
    [JsonPropertyName("route")]
    public string Route { get; init; } = string.Empty;

    [JsonPropertyName("dir")]
    public string Direction { get; init; } = string.Empty;

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
