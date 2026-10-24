using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services.Flights;

public interface IAirportCatalog
{
    DateTimeOffset GeneratedAt { get; }
    IReadOnlyList<AirportResponse> Search(string query, int limit, int offset = 0);
    AirportResponse? FindByIata(string iata);
}
