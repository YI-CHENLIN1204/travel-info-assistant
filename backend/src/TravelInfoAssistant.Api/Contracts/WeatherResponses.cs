namespace TravelInfoAssistant.Api.Contracts;

public sealed record WeatherResponse(
    string LocationName,
    double Latitude,
    double Longitude,
    string TimeZone,
    WeatherCurrentResponse Current,
    IReadOnlyList<WeatherHourlyForecastResponse> Hourly,
    IReadOnlyList<WeatherDailyForecastResponse> Daily);

public sealed record WeatherCurrentResponse(
    DateTimeOffset Time,
    double? TemperatureCelsius,
    double? ApparentTemperatureCelsius,
    double? PrecipitationMillimeters,
    double? PrecipitationProbabilityPercent,
    double? HumidityPercent,
    double? WindSpeedMetersPerSecond,
    double? WindFromDirectionDegrees,
    string ConditionCode,
    string ConditionLabel);

public sealed record WeatherHourlyForecastResponse(
    DateTimeOffset Time,
    double? TemperatureCelsius,
    double? PrecipitationMillimeters,
    double? PrecipitationProbabilityPercent,
    string ConditionCode,
    string ConditionLabel);

public sealed record WeatherDailyForecastResponse(
    DateOnly Date,
    double? MinimumTemperatureCelsius,
    double? MaximumTemperatureCelsius,
    double? PrecipitationMillimeters,
    double? PrecipitationProbabilityPercent,
    string ConditionCode,
    string ConditionLabel);
