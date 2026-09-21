using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services;

public interface ICityService
{
    Task<IReadOnlyList<CitySummaryResponse>> GetCitiesAsync(CancellationToken cancellationToken);
    Task<CitySummaryResponse?> GetCityAsync(Guid cityId, CancellationToken cancellationToken);
    Task<ResolveCityResponse> ResolveCityAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
