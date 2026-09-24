namespace TravelInfoAssistant.Api.Domain;

public sealed class EmergencyContact
{
    public Guid Id { get; set; }
    public required string CountryCode { get; set; }
    public string? CityCode { get; set; }
    public required string Category { get; set; }
    public required string DisplayName { get; set; }
    public required string PhoneNumber { get; set; }
    public string? Note { get; set; }
    public required string SourceName { get; set; }
    public required string SourceUrl { get; set; }
    public DateOnly VerifiedOn { get; set; }
    public int SortOrder { get; set; }
}
