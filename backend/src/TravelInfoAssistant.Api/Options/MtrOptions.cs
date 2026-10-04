namespace TravelInfoAssistant.Api.Options;

public sealed class MtrOptions
{
    public const string SectionName = "Mtr";

    public string StaticBaseUrl { get; set; } = "https://opendata.mtr.com.hk/data/";
    public string RealtimeBaseUrl { get; set; } = "https://rt.data.gov.hk/v1/transport/mtr/";
    public string ServiceHoursBaseUrl { get; set; } = "https://www.mtr.com.hk/ch/customer/services/";
    public string LinesAndStationsPath { get; set; } = "mtr_lines_and_stations.csv";
    public string ServiceHoursPath { get; set; } = "service_hours_search.php";
    public int TimeoutSeconds { get; set; } = 12;
}
