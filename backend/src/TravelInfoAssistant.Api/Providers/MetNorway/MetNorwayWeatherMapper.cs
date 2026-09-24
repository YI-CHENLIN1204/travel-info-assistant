using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public static class MetNorwayWeatherMapper
{
    public static WeatherResponse Map(
        MetNorwayForecast forecast,
        string locationName,
        string timeZone,
        double fallbackLatitude,
        double fallbackLongitude)
    {
        var series = forecast.Properties?.Timeseries
            .Where(item => item.Data?.Instant?.Details is not null)
            .OrderBy(item => item.Time)
            .ToList() ?? [];
        if (series.Count == 0)
        {
            throw new MetNorwayProviderException("MET Norway returned no usable forecast points.");
        }

        var zone = FindTimeZone(timeZone);
        var current = MapCurrent(series[0]);
        var hourly = series
            .Take(12)
            .Select(MapHourly)
            .ToList();
        var daily = series
            .GroupBy(item => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(item.Time, zone).DateTime))
            .OrderBy(group => group.Key)
            .Take(6)
            .Select(group => MapDaily(group.Key, group, zone))
            .ToList();

        var coordinates = forecast.Geometry?.Coordinates;
        var longitude = coordinates is { Count: >= 2 } ? coordinates[0] : fallbackLongitude;
        var latitude = coordinates is { Count: >= 2 } ? coordinates[1] : fallbackLatitude;

        return new WeatherResponse(
            locationName,
            Math.Round(latitude, 4),
            Math.Round(longitude, 4),
            timeZone,
            current,
            hourly,
            daily);
    }

    public static double? CalculateApparentTemperature(
        double? temperatureCelsius,
        double? humidityPercent,
        double? windSpeedMetersPerSecond)
    {
        if (temperatureCelsius is null)
        {
            return null;
        }

        if (humidityPercent is null || windSpeedMetersPerSecond is null)
        {
            return Round(temperatureCelsius);
        }

        var temperature = temperatureCelsius.Value;
        var vaporPressure = humidityPercent.Value / 100d * 6.105d *
                            Math.Exp(17.27d * temperature / (237.7d + temperature));
        var apparent = temperature + 0.33d * vaporPressure -
                       0.7d * windSpeedMetersPerSecond.Value - 4d;
        return Round(apparent);
    }

    public static string GetConditionLabel(string? symbolCode)
    {
        var normalized = NormalizeConditionCode(symbolCode);
        if (normalized.Contains("thunder", StringComparison.Ordinal)) return "雷雨";
        if (normalized.Contains("sleet", StringComparison.Ordinal)) return "雨夾雪";
        if (normalized.Contains("snow", StringComparison.Ordinal)) return "降雪";
        if (normalized.Contains("rain", StringComparison.Ordinal)) return "降雨";
        if (normalized.Contains("fog", StringComparison.Ordinal)) return "有霧";

        return normalized switch
        {
            "clearsky" => "晴朗",
            "fair" => "晴時多雲",
            "partlycloudy" => "局部多雲",
            "cloudy" => "多雲",
            _ => "天氣狀況"
        };
    }

    private static WeatherCurrentResponse MapCurrent(MetNorwayTimeSeries item)
    {
        var details = item.Data!.Instant!.Details!;
        var period = GetShortestPeriod(item.Data);
        var conditionCode = NormalizeConditionCode(period?.Summary?.SymbolCode);

        return new WeatherCurrentResponse(
            item.Time,
            Round(details.AirTemperature),
            CalculateApparentTemperature(
                details.AirTemperature,
                details.RelativeHumidity,
                details.WindSpeed),
            Round(period?.Details?.PrecipitationAmount),
            RoundWhole(period?.Details?.ProbabilityOfPrecipitation),
            RoundWhole(details.RelativeHumidity),
            Round(details.WindSpeed),
            RoundWhole(details.WindFromDirection),
            conditionCode,
            GetConditionLabel(conditionCode));
    }

    private static WeatherHourlyForecastResponse MapHourly(MetNorwayTimeSeries item)
    {
        var period = GetShortestPeriod(item.Data);
        var conditionCode = NormalizeConditionCode(period?.Summary?.SymbolCode);
        return new WeatherHourlyForecastResponse(
            item.Time,
            Round(item.Data?.Instant?.Details?.AirTemperature),
            Round(period?.Details?.PrecipitationAmount),
            RoundWhole(period?.Details?.ProbabilityOfPrecipitation),
            conditionCode,
            GetConditionLabel(conditionCode));
    }

    private static WeatherDailyForecastResponse MapDaily(
        DateOnly date,
        IEnumerable<MetNorwayTimeSeries> values,
        TimeZoneInfo timeZone)
    {
        var items = values.ToList();
        var temperatures = items
            .Select(item => item.Data?.Instant?.Details?.AirTemperature)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        var periods = items
            .Select(item => GetShortestPeriod(item.Data))
            .Where(item => item is not null)
            .Cast<MetNorwayPeriod>()
            .ToList();
        var precipitation = periods
            .Select(item => item.Details?.PrecipitationAmount)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        var probabilities = periods
            .Select(item => item.Details?.ProbabilityOfPrecipitation)
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToList();
        var representative = items
            .Where(item => !string.IsNullOrWhiteSpace(
                GetShortestPeriod(item.Data)?.Summary?.SymbolCode))
            .OrderBy(item => Math.Abs(
                TimeZoneInfo.ConvertTime(item.Time, timeZone).Hour - 12))
            .FirstOrDefault();
        var conditionCode = NormalizeConditionCode(
            GetShortestPeriod(representative?.Data)?.Summary?.SymbolCode);

        return new WeatherDailyForecastResponse(
            date,
            temperatures.Count == 0 ? null : Round(temperatures.Min()),
            temperatures.Count == 0 ? null : Round(temperatures.Max()),
            precipitation.Count == 0 ? null : Round(precipitation.Sum()),
            probabilities.Count == 0 ? null : RoundWhole(probabilities.Max()),
            conditionCode,
            GetConditionLabel(conditionCode));
    }

    private static MetNorwayPeriod? GetShortestPeriod(MetNorwayForecastData? data) =>
        data?.Next1Hours ?? data?.Next6Hours ?? data?.Next12Hours;

    private static string NormalizeConditionCode(string? symbolCode)
    {
        var value = symbolCode?.Trim().ToLowerInvariant() ?? string.Empty;
        foreach (var suffix in new[] { "_polartwilight", "_night", "_day" })
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value[..^suffix.Length];
            }
        }

        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private static TimeZoneInfo FindTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static double? Round(double? value) =>
        value.HasValue ? Math.Round(value.Value, 1) : null;

    private static double? RoundWhole(double? value) =>
        value.HasValue ? Math.Round(value.Value) : null;
}
