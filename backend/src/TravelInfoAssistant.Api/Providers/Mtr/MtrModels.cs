using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.Mtr;

public sealed record MtrStationRow(
    string LineCode,
    string Direction,
    string StationCode,
    string StationId,
    string NameZh,
    string NameEn,
    int Sequence);

public sealed class MtrScheduleResponse
{
    [JsonPropertyName("sys_time")]
    public string? SystemTime { get; init; }

    [JsonPropertyName("curr_time")]
    public string? CurrentTime { get; init; }

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, MtrStationSchedule> Data { get; init; } =
        new Dictionary<string, MtrStationSchedule>();

    [JsonPropertyName("isdelay")]
    public string? IsDelay { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed class MtrStationSchedule
{
    [JsonPropertyName("sys_time")]
    public string? SystemTime { get; init; }

    [JsonPropertyName("curr_time")]
    public string? CurrentTime { get; init; }

    [JsonPropertyName("UP")]
    public IReadOnlyList<MtrTrainPrediction> Up { get; init; } = [];

    [JsonPropertyName("DOWN")]
    public IReadOnlyList<MtrTrainPrediction> Down { get; init; } = [];
}

public sealed class MtrTrainPrediction
{
    [JsonPropertyName("seq")]
    public string? Sequence { get; init; }

    [JsonPropertyName("dest")]
    public string? DestinationCode { get; init; }

    [JsonPropertyName("plat")]
    public string? Platform { get; init; }

    [JsonPropertyName("time")]
    public string? Time { get; init; }

    [JsonPropertyName("valid")]
    public string? Valid { get; init; }
}
