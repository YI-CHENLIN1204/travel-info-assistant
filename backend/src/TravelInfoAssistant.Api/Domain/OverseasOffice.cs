namespace TravelInfoAssistant.Api.Domain;

public sealed class OverseasOffice
{
    public Guid Id { get; set; }
    public required string CountryCode { get; set; }
    public string? CityCode { get; set; }
    public required string NameZh { get; set; }
    public required string Address { get; set; }
    public required string MainPhone { get; set; }
    public required string EmergencyPhone { get; set; }
    public string? Note { get; set; }
    public required string SourceName { get; set; }
    public required string SourceUrl { get; set; }
    public DateOnly VerifiedOn { get; set; }
}
