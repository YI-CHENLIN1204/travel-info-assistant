namespace TravelInfoAssistant.Api.Options;

public sealed class BocaOptions
{
    public const string SectionName = "Boca";

    public string BaseUrl { get; init; } = "https://www.boca.gov.tw/";
    public string RssPath { get; init; } = "sp-trwa-rss-1.xml";
    public string UserAgent { get; init; } =
        "TravelInfoAssistant/0.1 https://github.com/YI-CHENLIN1204/travel-info-assistant";
    public int TimeoutSeconds { get; init; } = 15;
}
