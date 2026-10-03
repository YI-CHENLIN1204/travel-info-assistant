using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class ProviderCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_WhenRefreshFails_ReturnsRetainedDataAsStale()
    {
        var now = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new AdjustableTimeProvider(now);
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();

        await using var serviceProvider = services.BuildServiceProvider();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ProviderCache(
            serviceProvider.GetRequiredService<IDistributedCache>(),
            memoryCache,
            timeProvider,
            NullLogger<ProviderCache>.Instance);

        var initial = await cache.GetOrCreateAsync(
            "provider:test",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromHours(1),
            _ => Task.FromResult(new ProviderPayload<string>(
                "retained data",
                "realtime",
                now.AddMinutes(-1),
                now)),
            CancellationToken.None);

        timeProvider.Advance(TimeSpan.FromMinutes(2));
        var refreshAttempts = 0;

        var fallback = await cache.GetOrCreateAsync<string>(
            "provider:test",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromHours(1),
            _ =>
            {
                refreshAttempts++;
                throw new InvalidOperationException("Provider unavailable for test.");
            },
            CancellationToken.None);

        Assert.Equal("retained data", initial.Data);
        Assert.Equal(1, refreshAttempts);
        Assert.Equal("retained data", fallback.Data);
        Assert.Equal("cached", fallback.DataStatus);
        Assert.True(fallback.Stale);
        Assert.Equal(
            "資料來源暫時無法更新，目前顯示上次成功取得的資料。",
            fallback.Message);
        Assert.Equal(now.AddMinutes(-1), fallback.SourceUpdatedAt);
        Assert.Equal(now, fallback.FetchedAt);
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow = utcNow.Add(duration);
    }
}
