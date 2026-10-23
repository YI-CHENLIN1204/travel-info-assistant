using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.LocalAlerts;

public interface ILocalAlertsProvider
{
    string CountryCode { get; }

    Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city,
        CancellationToken cancellationToken);
}
