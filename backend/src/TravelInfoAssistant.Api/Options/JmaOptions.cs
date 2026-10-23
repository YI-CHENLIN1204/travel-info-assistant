namespace TravelInfoAssistant.Api.Options;

public sealed class JmaOptions
{
    public const string SectionName = "Jma";

    public string BaseUrl { get; init; } = "https://www.data.jma.go.jp/developer/xml/";
    public string WeatherFeedPath { get; init; } = "feed/extra_l.xml";
    public string EarthquakeVolcanoFeedPath { get; init; } = "feed/eqvol_l.xml";
    public string UserAgent { get; init; } =
        "TravelInfoAssistant/0.1 https://github.com/YI-CHENLIN1204/travel-info-assistant";
    public int TimeoutSeconds { get; init; } = 20;
    public int MaxEarthquakeDocuments { get; init; } = 80;
    public int MaxTsunamiDocuments { get; init; } = 20;
    public int MaxVolcanoDocuments { get; init; } = 40;
}
