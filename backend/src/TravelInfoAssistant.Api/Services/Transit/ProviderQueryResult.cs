namespace TravelInfoAssistant.Api.Services.Transit;

public sealed record ProviderQueryResult<T>(
    T Data,
    string DataStatus,
    DateTimeOffset? SourceUpdatedAt,
    DateTimeOffset FetchedAt,
    bool Stale,
    string? Message,
    string Source = "TDX",
    string? ServiceDayStatus = null,
    DateTimeOffset? LastDepartureAt = null,
    string? LastDepartureDescription = null)
{
    public static ProviderQueryResult<T> Unavailable(
        T fallback,
        string message,
        TimeProvider timeProvider,
        string source = "TDX") =>
        new(
            fallback,
            "unavailable",
            null,
            timeProvider.GetUtcNow(),
            false,
            message,
            source);
}
