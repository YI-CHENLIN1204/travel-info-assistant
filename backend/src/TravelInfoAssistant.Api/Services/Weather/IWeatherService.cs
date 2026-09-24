using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Weather;

public interface IWeatherService
{
    Task<ProviderQueryResult<WeatherResponse?>?> GetCityWeatherAsync(
        Guid cityId,
        CancellationToken cancellationToken);

    Task<ProviderQueryResult<WeatherResponse?>> GetLocationWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
