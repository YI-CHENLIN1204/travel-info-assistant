using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public interface IAeroDataBoxFlightProvider
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
}
