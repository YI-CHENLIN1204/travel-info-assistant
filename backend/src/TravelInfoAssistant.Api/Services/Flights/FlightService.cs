using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.AeroDataBox;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Flights;

public sealed class FlightService(
    IAeroDataBoxFlightProvider provider,
    IAeroDataBoxUsageMeter usageMeter) : IFlightService
{
    public Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> SearchRouteAsync(
        string originIata,
        string destinationIata,
        DateOnly date,
        CancellationToken cancellationToken) =>
        provider.SearchRouteAsync(originIata, destinationIata, date, cancellationToken);

    public Task<ProviderQueryResult<IReadOnlyList<FlightItineraryResponse>>> GetFlightAsync(
        string flightNumber,
        DateOnly date,
        CancellationToken cancellationToken) =>
        provider.GetFlightAsync(flightNumber, date, cancellationToken);

    public Task<AeroDataBoxProviderStatusResponse> GetProviderStatusAsync(
        CancellationToken cancellationToken) =>
        usageMeter.GetStatusAsync(cancellationToken);
}
