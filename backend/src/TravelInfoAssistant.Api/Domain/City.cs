namespace TravelInfoAssistant.Api.Domain;

public sealed class City
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string NameZh { get; set; }
    public required string NameEn { get; set; }
    public required string CountryCode { get; set; }
    public required string TimeZone { get; set; }
    public double CenterLatitude { get; set; }
    public double CenterLongitude { get; set; }
    public double CoverageRadiusKilometers { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<CityServiceCapability> ServiceCapabilities { get; set; } = [];
}
