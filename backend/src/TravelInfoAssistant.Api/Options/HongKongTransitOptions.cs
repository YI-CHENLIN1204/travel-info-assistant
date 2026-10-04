namespace TravelInfoAssistant.Api.Options;

public sealed class HongKongTransitOptions
{
    public const string SectionName = "HongKongTransit";

    public string ScheduleUrl { get; set; } = "https://static.data.gov.hk/td/pt-headway-tc/gtfs.zip";
    public int TimeoutSeconds { get; set; } = 20;
}
