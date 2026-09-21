namespace TravelInfoAssistant.Api.Contracts;

public sealed record HealthResponse(
    string Status,
    DependencyStatus Database,
    DependencyStatus Redis,
    DateTimeOffset CheckedAt);

public sealed record DependencyStatus(bool Available, string Status);
