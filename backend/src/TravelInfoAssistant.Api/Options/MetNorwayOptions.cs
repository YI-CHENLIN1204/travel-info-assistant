namespace TravelInfoAssistant.Api.Options;

public sealed class MetNorwayOptions
{
    public const string SectionName = "MetNorway";

    public string BaseUrl { get; set; } =
        "https://api.met.no/weatherapi/locationforecast/2.0/";

    public string UserAgent { get; set; } =
        "TravelInfoAssistant/0.1 https://github.com/YI-CHENLIN1204/travel-info-assistant";

    public int TimeoutSeconds { get; set; } = 12;
    public int RequestsPerSecond { get; set; } = 10;
}
