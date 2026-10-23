namespace TravelInfoAssistant.Api.Options;

public sealed class NcdrOptions
{
    public const string SectionName = "Ncdr";

    public string BaseUrl { get; init; } = "https://alerts.ncdr.nat.gov.tw/";
    public string ActiveFeedPath { get; init; } = "webapi/RssAtomFeed.ashx";
    public string UserAgent { get; init; } =
        "TravelInfoAssistant/0.1 https://github.com/YI-CHENLIN1204/travel-info-assistant";
    public int TimeoutSeconds { get; init; } = 15;
}
