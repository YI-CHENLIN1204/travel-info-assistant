using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services.Transit;

public interface ITransitService
{
    Task<IReadOnlyList<TransitModeResponse>?> GetModesAsync(
        Guid cityId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> SearchBusRoutesAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        Guid cityId,
        string routeName,
        int direction,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        Guid cityId,
        string routeName,
        int direction,
        string stopId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<MetroStationResponse>>> SearchMetroStationsAsync(
        Guid cityId,
        string? query,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetMetroArrivalsAsync(
        Guid cityId,
        string stationId,
        CancellationToken cancellationToken);

    Task<TdxProviderStatusResponse> GetTdxStatusAsync(CancellationToken cancellationToken);
}
