using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Infrastructure;
using TravelInfoAssistant.Api.Providers.MetNorway;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Weather;

public sealed class WeatherService(
    AppDbContext dbContext,
    IMetNorwayWeatherProvider provider) : IWeatherService
{
    public async Task<ProviderQueryResult<WeatherResponse?>?> GetCityWeatherAsync(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await dbContext.Cities
            .AsNoTracking()
            .Where(item => item.IsActive)
            .FirstOrDefaultAsync(item => item.Id == cityId, cancellationToken);
        if (city is null)
        {
            return null;
        }

        return await provider.GetForecastAsync(
            city.NameZh,
            city.CenterLatitude,
            city.CenterLongitude,
            city.TimeZone,
            cancellationToken);
    }

    public async Task<ProviderQueryResult<WeatherResponse?>> GetLocationWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var cities = await dbContext.Cities
            .AsNoTracking()
            .Where(item => item.IsActive)
            .ToListAsync(cancellationToken);
        var nearest = FindNearestSupportedCity(cities, latitude, longitude);

        return await provider.GetForecastAsync(
            "目前位置",
            latitude,
            longitude,
            nearest?.TimeZone ?? "UTC",
            cancellationToken);
    }

    private static City? FindNearestSupportedCity(
        IEnumerable<City> cities,
        double latitude,
        double longitude) =>
        cities
            .Select(city => new
            {
                City = city,
                Distance = GeoDistance.CalculateKilometers(
                    latitude,
                    longitude,
                    city.CenterLatitude,
                    city.CenterLongitude)
            })
            .Where(item => item.Distance <= item.City.CoverageRadiusKilometers)
            .OrderBy(item => item.Distance)
            .Select(item => item.City)
            .FirstOrDefault();
}
