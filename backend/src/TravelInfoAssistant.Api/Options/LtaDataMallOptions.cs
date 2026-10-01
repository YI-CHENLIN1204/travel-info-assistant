namespace TravelInfoAssistant.Api.Options;

public sealed class LtaDataMallOptions
{
    public const string SectionName = "LtaDataMall";

    public string BaseUrl { get; set; } =
        "https://datamall2.mytransport.sg/ltaodataservice/";

    public string ChineseNamesUrl { get; set; } =
        "https://datamall.lta.gov.sg/content/dam/datamall/datasets/Geospatial/" +
        "Train%20Station%20Codes%20and%20Chinese%20Names.zip";

    public string AccountKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 15;
}
