using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Flights;

public interface IFlightService
{
    Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> SearchRouteAsync(
        string originIata,
        string destinationIata,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> GetFlightAsync(
        string flightNumber,
        DateOnly date,
        CancellationToken cancellationToken);

    Task<AeroDataBoxProviderStatusResponse> GetProviderStatusAsync(
        CancellationToken cancellationToken);
}
