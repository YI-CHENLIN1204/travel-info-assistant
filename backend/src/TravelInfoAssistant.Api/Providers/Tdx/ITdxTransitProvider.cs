using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public interface ITdxTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string routeName,
        int direction,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string routeName,
        int direction,
        string stopId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> GetMetroStationsAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<RailStationResponse>>> GetRailStationsAsync(
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetRailArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken);
}
