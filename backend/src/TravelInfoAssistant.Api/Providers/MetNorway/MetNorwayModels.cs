using System.Text.Json.Serialization;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public sealed class MetNorwayForecast
{
    public MetNorwayGeometry? Geometry { get; init; }
    public MetNorwayProperties? Properties { get; init; }
}

public sealed class MetNorwayGeometry
{
    public IReadOnlyList<double> Coordinates { get; init; } = [];
}

public sealed class MetNorwayProperties
{
    public MetNorwayMeta? Meta { get; init; }
    public IReadOnlyList<MetNorwayTimeSeries> Timeseries { get; init; } = [];
}

public sealed class MetNorwayMeta
{
    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }
}

public sealed class MetNorwayTimeSeries
{
    public DateTimeOffset Time { get; init; }
    public MetNorwayForecastData? Data { get; init; }
}

public sealed class MetNorwayForecastData
{
    public MetNorwayInstant? Instant { get; init; }

    [JsonPropertyName("next_1_hours")]
    public MetNorwayPeriod? Next1Hours { get; init; }

    [JsonPropertyName("next_6_hours")]
    public MetNorwayPeriod? Next6Hours { get; init; }

    [JsonPropertyName("next_12_hours")]
    public MetNorwayPeriod? Next12Hours { get; init; }
}

public sealed class MetNorwayInstant
{
    public MetNorwayInstantDetails? Details { get; init; }
}

public sealed class MetNorwayInstantDetails
{
    [JsonPropertyName("air_temperature")]
    public double? AirTemperature { get; init; }

    [JsonPropertyName("relative_humidity")]
    public double? RelativeHumidity { get; init; }

    [JsonPropertyName("wind_speed")]
    public double? WindSpeed { get; init; }

    [JsonPropertyName("wind_from_direction")]
    public double? WindFromDirection { get; init; }
}

public sealed class MetNorwayPeriod
{
    public MetNorwaySummary? Summary { get; init; }
    public MetNorwayPeriodDetails? Details { get; init; }
}

public sealed class MetNorwaySummary
{
    [JsonPropertyName("symbol_code")]
    public string? SymbolCode { get; init; }
}

public sealed class MetNorwayPeriodDetails
{
    [JsonPropertyName("air_temperature_max")]
    public double? AirTemperatureMaximum { get; init; }

    [JsonPropertyName("air_temperature_min")]
    public double? AirTemperatureMinimum { get; init; }

    [JsonPropertyName("precipitation_amount")]
    public double? PrecipitationAmount { get; init; }

    [JsonPropertyName("probability_of_precipitation")]
    public double? ProbabilityOfPrecipitation { get; init; }
}
