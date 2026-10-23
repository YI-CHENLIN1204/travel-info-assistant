namespace TravelInfoAssistant.Api.Options;

public sealed class HkoOptions
{
    public const string SectionName = "Hko";

    public string BaseUrl { get; init; } = "https://data.weather.gov.hk/weatherAPI/opendata/";
    public string WarningSummaryPath { get; init; } = "weather.php?dataType=warnsum&lang=tc";
    public string WarningInfoPath { get; init; } = "weather.php?dataType=warningInfo&lang=tc";
    public string SpecialWeatherTipsPath { get; init; } = "weather.php?dataType=swt&lang=tc";
    public string FeltEarthquakePath { get; init; } =
        "earthquake.php?dataType=feltearthquake&lang=tc";
    public string UserAgent { get; init; } =
        "TravelInfoAssistant/0.1 https://github.com/YI-CHENLIN1204/travel-info-assistant";
    public int TimeoutSeconds { get; init; } = 15;
}
