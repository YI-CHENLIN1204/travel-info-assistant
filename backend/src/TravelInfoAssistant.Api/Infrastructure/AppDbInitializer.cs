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

        if (changed)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
