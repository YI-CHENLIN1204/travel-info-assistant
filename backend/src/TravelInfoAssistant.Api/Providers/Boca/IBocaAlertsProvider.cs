using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Boca;

public interface IBocaAlertsProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>> GetAlertsAsync(
        CancellationToken cancellationToken);
}
