namespace TravelInfoAssistant.Api.Services.Transit;

public sealed record ProviderQueryResult<T>(
    T Data,
    string DataStatus,
    DateTimeOffset? SourceUpdatedAt,
    DateTimeOffset FetchedAt,
    bool Stale,
    string? Message)
{
    public static ProviderQueryResult<T> Unavailable(
        T fallback,
        string message,
        TimeProvider timeProvider) =>
        new(
            fallback,
            "unavailable",
            null,
            timeProvider.GetUtcNow(),
            false,
            message);
}
