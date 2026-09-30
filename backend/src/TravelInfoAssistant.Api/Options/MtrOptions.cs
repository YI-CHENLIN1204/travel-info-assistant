namespace TravelInfoAssistant.Api.Options;

public sealed class MtrOptions
{
    public const string SectionName = "Mtr";

    public string StaticBaseUrl { get; set; } = "https://opendata.mtr.com.hk/data/";
    public string RealtimeBaseUrl { get; set; } = "https://rt.data.gov.hk/v1/transport/mtr/";
    public string LinesAndStationsPath { get; set; } = "mtr_lines_and_stations.csv";
    public int TimeoutSeconds { get; set; } = 12;
}
