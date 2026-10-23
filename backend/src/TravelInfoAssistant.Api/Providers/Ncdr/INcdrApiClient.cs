namespace TravelInfoAssistant.Api.Providers.Ncdr;

public interface INcdrApiClient
{
    Task<NcdrAtomDocument> GetActiveAlertsAsync(
        string county,
        CancellationToken cancellationToken);
}

public sealed record NcdrAtomDocument(string Xml, DateTimeOffset FetchedAt);
