namespace TravelInfoAssistant.Api.Options;

public sealed class KmbOptions
{
    public const string SectionName = "Kmb";

    public string BaseUrl { get; set; } = "https://data.etabus.gov.hk/v1/transport/kmb/";
    public string ScheduleUrl { get; set; } = "https://static.data.gov.hk/td/pt-headway-tc/gtfs.zip";
    public int TimeoutSeconds { get; set; } = 15;
}
