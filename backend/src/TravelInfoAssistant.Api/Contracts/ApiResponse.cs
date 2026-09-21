namespace TravelInfoAssistant.Api.Contracts;

public sealed record ApiResponse<T>(T Data, ApiMeta Meta)
{
    public static ApiResponse<T> Success(
        T data,
        string source = "Internal",
        string dataStatus = "scheduled",
        string? message = null) =>
        new(
            data,
            new ApiMeta(
                dataStatus,
                source,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                false,
                message));
}

public sealed record ApiMeta(
    string DataStatus,
    string Source,
    DateTimeOffset? SourceUpdatedAt,
    DateTimeOffset FetchedAt,
    bool Stale,
    string? Message);
