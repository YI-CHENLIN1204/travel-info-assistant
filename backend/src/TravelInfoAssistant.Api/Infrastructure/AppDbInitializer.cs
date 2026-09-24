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

        var integratedDefinitions = new[]
        {
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("2ad9a7d4-b49b-4da5-a554-f046f00689b8"),
                ServiceKey = "weather",
                DisplayName = "天氣",
                SortOrder = 4
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("bf60bfe0-0e19-43a3-86dc-434b3f6bba5d"),
                ServiceKey = "weather",
                DisplayName = "天氣",
                SortOrder = 3
            },
            new
            {
                CityCode = "taipei",
                Id = Guid.Parse("a93d797c-d305-4a82-a082-f2bc5279d1fb"),
                ServiceKey = "alerts",
                DisplayName = "旅遊警示",
                SortOrder = 5
            },
            new
            {
                CityCode = "tokyo",
                Id = Guid.Parse("05c4a5b2-5c5e-477d-a968-95f96276bb31"),
                ServiceKey = "alerts",
                DisplayName = "旅遊警示",
                SortOrder = 4
            }
        };
        foreach (var definition in integratedDefinitions)
        {
            var city = await dbContext.Cities
                .FirstOrDefaultAsync(item => item.Code == definition.CityCode);
            if (city is null)
            {
                continue;
            }

            var capability = await dbContext.CityServiceCapabilities
                .FirstOrDefaultAsync(item =>
                    item.CityId == city.Id && item.ServiceKey == definition.ServiceKey);
            if (capability is null)
            {
                dbContext.CityServiceCapabilities.Add(new CityServiceCapability
                {
                    Id = definition.Id,
                    CityId = city.Id,
                    ServiceKey = definition.ServiceKey,
                    DisplayName = definition.DisplayName,
                    IntegrationStatus = IntegrationStatus.Integrated,
                    AvailabilityStatus = AvailabilityStatus.Available,
                    SortOrder = definition.SortOrder
                });
                changed = true;
                continue;
            }

            if (capability.IntegrationStatus != IntegrationStatus.Integrated ||
                capability.AvailabilityStatus != AvailabilityStatus.Available)
            {
                capability.IntegrationStatus = IntegrationStatus.Integrated;
                capability.AvailabilityStatus = AvailabilityStatus.Available;
                changed = true;
            }
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
