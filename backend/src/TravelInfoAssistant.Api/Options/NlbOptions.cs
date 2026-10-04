namespace TravelInfoAssistant.Api.Options;

public sealed class NlbOptions
{
    public const string SectionName = "Nlb";

    public string BaseUrl { get; set; } = "https://rt.data.gov.hk/v2/transport/nlb/";
    public int TimeoutSeconds { get; set; } = 15;
}
