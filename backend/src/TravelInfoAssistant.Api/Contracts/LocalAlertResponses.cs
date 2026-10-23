namespace TravelInfoAssistant.Api.Contracts;

public sealed record LocalAlertResponse(
    string Id,
    string Kind,
    string Category,
    string Severity,
    string SeverityLabel,
    string Title,
    string Summary,
    string RegionName,
    string SourceName,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? EffectiveAt,
    DateTimeOffset? ExpiresAt,
    string SourceUrl);
