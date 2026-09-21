namespace TravelInfoAssistant.Api.Domain;

public sealed class CityServiceCapability
{
    public Guid Id { get; set; }
    public Guid CityId { get; set; }
    public required string ServiceKey { get; set; }
    public required string DisplayName { get; set; }
    public IntegrationStatus IntegrationStatus { get; set; }
    public AvailabilityStatus AvailabilityStatus { get; set; }
    public string? Message { get; set; }
    public int SortOrder { get; set; }

    public City City { get; set; } = null!;
}

public enum IntegrationStatus
{
    Integrated,
    NotIntegrated,
    ExplicitlyUnsupported
}

public enum AvailabilityStatus
{
    Available,
    TemporarilyUnavailable
}
