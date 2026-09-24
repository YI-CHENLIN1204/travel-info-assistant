using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Alerts;

public interface ITravelAlertService
{
    Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>?> GetCityAlertsAsync(
        Guid cityId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>> GetPopularAlertsAsync(
        CancellationToken cancellationToken);
}
