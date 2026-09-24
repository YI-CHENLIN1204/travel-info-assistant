namespace TravelInfoAssistant.Api.Domain;

public sealed class EmergencyGuide
{
    public Guid Id { get; set; }
    public required string CountryCode { get; set; }
    public required string Slug { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string StepsJson { get; set; }
    public required string SourceName { get; set; }
    public required string SourceUrl { get; set; }
    public DateOnly VerifiedOn { get; set; }
    public int SortOrder { get; set; }
}
