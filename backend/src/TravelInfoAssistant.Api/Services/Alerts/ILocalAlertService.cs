using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Alerts;

public interface ILocalAlertService
{
    Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>?> GetCityAlertsAsync(
        Guid cityId,
        CancellationToken cancellationToken);
}
