using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Domain;

namespace TravelInfoAssistant.Api.Infrastructure;

public static class AppDbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const int maximumAttempts = 5;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
                await ApplyCurrentCapabilityStateAsync(dbContext);
                return;
            }
            catch (Exception exception) when (attempt < maximumAttempts)
            {
                logger.LogWarning(
                    exception,
                    "Database initialization attempt {Attempt}/{MaximumAttempts} failed.",
                    attempt,
                    maximumAttempts);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }
    }

    private static async Task ApplyCurrentCapabilityStateAsync(AppDbContext dbContext)
    {
        var taipeiTransit = await dbContext.CityServiceCapabilities
            .Where(item => item.City.Code == "taipei")
            .Where(item => item.ServiceKey == "bus" || item.ServiceKey == "metro" ||
                           item.ServiceKey == "rail")
            .ToListAsync();

        var changed = false;
        foreach (var capability in taipeiTransit)
        {
            if (capability.IntegrationStatus == IntegrationStatus.Integrated)
            {
                continue;
            }

            capability.IntegrationStatus = IntegrationStatus.Integrated;
            changed = true;
        }

        var weatherDefinitions = new[]
        {
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("2ad9a7d4-b49b-4da5-a554-f046f00689b8"),
                SortOrder = 4
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("bf60bfe0-0e19-43a3-86dc-434b3f6bba5d"),
                SortOrder = 3
            }
        };
        foreach (var definition in weatherDefinitions)
        {
            var city = await dbContext.Cities
                .FirstOrDefaultAsync(item => item.Code == definition.CityCode);
            if (city is null)
            {
                continue;
            }

            var weather = await dbContext.CityServiceCapabilities
                .FirstOrDefaultAsync(item =>
                    item.CityId == city.Id && item.ServiceKey == "weather");
            if (weather is null)
            {
                dbContext.CityServiceCapabilities.Add(new CityServiceCapability
                {
                    Id = definition.Id,
                    CityId = city.Id,
                    ServiceKey = "weather",
                    DisplayName = "天氣",
                    IntegrationStatus = IntegrationStatus.Integrated,
                    AvailabilityStatus = AvailabilityStatus.Available,
                    SortOrder = definition.SortOrder
                });
                changed = true;
                continue;
            }

            if (weather.IntegrationStatus != IntegrationStatus.Integrated ||
                weather.AvailabilityStatus != AvailabilityStatus.Available)
            {
                weather.IntegrationStatus = IntegrationStatus.Integrated;
                weather.AvailabilityStatus = AvailabilityStatus.Available;
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
