using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public interface IMetNorwayWeatherProvider
{
    Task<ProviderQueryResult<WeatherResponse?>> GetForecastAsync(
        string locationName,
        double latitude,
        double longitude,
        string timeZone,
        CancellationToken cancellationToken);
}
