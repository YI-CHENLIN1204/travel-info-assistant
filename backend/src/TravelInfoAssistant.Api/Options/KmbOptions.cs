namespace TravelInfoAssistant.Api.Options;

public sealed class KmbOptions
{
    public const string SectionName = "Kmb";

    public string BaseUrl { get; set; } = "https://data.etabus.gov.hk/v1/transport/kmb/";
    public int TimeoutSeconds { get; set; } = 15;
}
