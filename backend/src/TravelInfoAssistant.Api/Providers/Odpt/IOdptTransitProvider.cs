using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public interface IOdptTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetMetroRoutesAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroDeparturesAsync(
        string stationId,
        CancellationToken cancellationToken);
}
