using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Infrastructure;

namespace TravelInfoAssistant.Api.Services;

public sealed class SystemHealthService(
    AppDbContext dbContext,
    IDistributedCache cache,
    ILogger<SystemHealthService> logger) : ISystemHealthService
{
    public async Task<HealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var database = await CheckDatabaseAsync(cancellationToken);
        var redis = await CheckRedisAsync(cancellationToken);
        var healthy = database.Available && redis.Available;

        return new HealthResponse(
            healthy ? "healthy" : "degraded",
            database,
            redis,
            DateTimeOffset.UtcNow);
    }

    private async Task<DependencyStatus> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var available = await dbContext.Database.CanConnectAsync(cancellationToken);
            return new DependencyStatus(available, available ? "connected" : "unavailable");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PostgreSQL health check failed.");
            return new DependencyStatus(false, "unavailable");
        }
    }

    private async Task<DependencyStatus> CheckRedisAsync(CancellationToken cancellationToken)
    {
        try
        {
            const string key = "health:last-check";
            var value = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            await cache.SetStringAsync(
                key,
                value,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
                },
                cancellationToken);
            var cached = await cache.GetStringAsync(key, cancellationToken);
            var available = cached == value;
            return new DependencyStatus(available, available ? "connected" : "unavailable");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis health check failed.");
            return new DependencyStatus(false, "unavailable");
        }
    }
}
