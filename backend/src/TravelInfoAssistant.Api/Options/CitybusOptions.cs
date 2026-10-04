namespace TravelInfoAssistant.Api.Options;

public sealed class CitybusOptions
{
    public const string SectionName = "Citybus";

    public string BaseUrl { get; set; } = "https://rt.data.gov.hk/v1/transport/citybus-nwfb/";
    public int TimeoutSeconds { get; set; } = 15;
}
