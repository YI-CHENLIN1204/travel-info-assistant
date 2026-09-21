using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Infrastructure;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Services;

public sealed class CityService(
    AppDbContext dbContext,
    IOptions<TdxOptions> tdxOptions) : ICityService
{
    public async Task<IReadOnlyList<CitySummaryResponse>> GetCitiesAsync(
        CancellationToken cancellationToken)
    {
        var cities = await QueryCities()
            .OrderBy(city => city.SortOrder)
            .ToListAsync(cancellationToken);

        return cities.Select(ToResponse).ToList();
    }

    public async Task<CitySummaryResponse?> GetCityAsync(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await QueryCities()
            .FirstOrDefaultAsync(item => item.Id == cityId, cancellationToken);

        return city is null ? null : ToResponse(city);
    }

    public async Task<ResolveCityResponse> ResolveCityAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var cities = await QueryCities().ToListAsync(cancellationToken);

        var nearest = cities
            .Select(city => new
            {
                City = city,
                Distance = GeoDistance.CalculateKilometers(
                    latitude,
                    longitude,
                    city.CenterLatitude,
                    city.CenterLongitude)
            })
            .OrderBy(item => item.Distance)
            .FirstOrDefault();

        if (nearest is null || nearest.Distance > nearest.City.CoverageRadiusKilometers)
        {
            return new ResolveCityResponse(false, null, null);
        }

        return new ResolveCityResponse(
            true,
            ToResponse(nearest.City),
            Math.Round(nearest.Distance, 1));
    }

    private IQueryable<City> QueryCities() =>
        dbContext.Cities
            .AsNoTracking()
            .Where(city => city.IsActive)
            .Include(city => city.ServiceCapabilities.OrderBy(capability => capability.SortOrder));

    private CitySummaryResponse ToResponse(City city) =>
        new(
            city.Id,
            city.Code,
            city.NameZh,
            city.NameEn,
            city.CountryCode,
            city.TimeZone,
            city.CenterLatitude,
            city.CenterLongitude,
            city.ServiceCapabilities
                .OrderBy(capability => capability.SortOrder)
                .Select(capability => ToCapabilityResponse(city, capability))
                .ToList());

    private ServiceCapabilityResponse ToCapabilityResponse(
        City city,
        CityServiceCapability capability)
    {
        var requiresTdx = city.Code == "taipei" &&
                          capability.ServiceKey is "bus" or "metro" &&
                          capability.IntegrationStatus == IntegrationStatus.Integrated;
        if (requiresTdx && !tdxOptions.Value.IsConfigured)
        {
            return new ServiceCapabilityResponse(
                capability.ServiceKey,
                capability.DisplayName,
                ToCamelCase(capability.IntegrationStatus),
                ToCamelCase(AvailabilityStatus.TemporarilyUnavailable),
                "TDX 已完成介接，但部署環境尚未設定 API 金鑰。");
        }

        return new ServiceCapabilityResponse(
            capability.ServiceKey,
            capability.DisplayName,
            ToCamelCase(capability.IntegrationStatus),
            ToCamelCase(capability.AvailabilityStatus),
            capability.Message);
    }

    private static string ToCamelCase<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var text = value.ToString();
        return char.ToLowerInvariant(text[0]) + text[1..];
    }
}
