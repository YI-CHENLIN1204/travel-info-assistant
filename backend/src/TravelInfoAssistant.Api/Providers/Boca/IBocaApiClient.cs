namespace TravelInfoAssistant.Api.Providers.Boca;

public interface IBocaApiClient
{
    Task<BocaRssDocument> GetAlertsAsync(CancellationToken cancellationToken);
}
