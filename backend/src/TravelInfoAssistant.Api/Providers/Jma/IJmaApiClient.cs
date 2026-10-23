namespace TravelInfoAssistant.Api.Providers.Jma;

public interface IJmaApiClient
{
    Task<JmaDocumentBundle> GetTokyoAlertDocumentsAsync(
        CancellationToken cancellationToken);
}

public sealed record JmaBulletinDocument(
    string Xml,
    string SourceUrl,
    DateTimeOffset UpdatedAt);

public sealed record JmaDocumentBundle(
    IReadOnlyList<JmaBulletinDocument> Documents,
    DateTimeOffset FetchedAt);
