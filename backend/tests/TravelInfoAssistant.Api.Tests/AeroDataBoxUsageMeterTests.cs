using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.AeroDataBox;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class AeroDataBoxUsageMeterTests
{
    [Fact]
    public async Task RouteSearchStopsAtSoftLimitWhileFlightStatusCanUseReserve()
    {
        var meter = CreateMeter(new AeroDataBoxOptions
        {
            ApiKey = "test-key",
            MonthlySoftLimitUnits = 4,
            MonthlyHardLimitUnits = 6,
            Tier2RequestUnits = 2,
            MonthlyTrafficSoftLimitBytes = 10_000
        });

        Assert.True(await meter.TryReserveAsync(
            AeroDataBoxRequestKind.RouteSearch,
            2,
            CancellationToken.None));
        Assert.True(await meter.TryReserveAsync(
            AeroDataBoxRequestKind.RouteSearch,
            2,
            CancellationToken.None));
        Assert.False(await meter.TryReserveAsync(
            AeroDataBoxRequestKind.RouteSearch,
            2,
            CancellationToken.None));
        Assert.True(await meter.TryReserveAsync(
            AeroDataBoxRequestKind.FlightStatus,
            2,
            CancellationToken.None));
        Assert.False(await meter.TryReserveAsync(
            AeroDataBoxRequestKind.FlightStatus,
            2,
            CancellationToken.None));

        var status = await meter.GetStatusAsync(CancellationToken.None);
        Assert.Equal(3, status.RequestCount);
        Assert.Equal(6, status.UsedUnits);
    }

    [Fact]
    public async Task TrafficStopPreventsAnotherReservation()
    {
        var meter = CreateMeter(new AeroDataBoxOptions
        {
            ApiKey = "test-key",
            MonthlySoftLimitUnits = 10,
            MonthlyHardLimitUnits = 10,
            MonthlyTrafficSoftLimitBytes = 100
        });
        await meter.AddResponseBytesAsync(100, CancellationToken.None);

        var reserved = await meter.TryReserveAsync(
            AeroDataBoxRequestKind.FlightStatus,
            2,
            CancellationToken.None);

        Assert.False(reserved);
    }

    private static AeroDataBoxUsageMeter CreateMeter(AeroDataBoxOptions settings)
    {
        var distributed = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var memory = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        return new AeroDataBoxUsageMeter(
            distributed,
            memory,
            Options.Create(settings),
            TimeProvider.System,
            NullLogger<AeroDataBoxUsageMeter>.Instance);
    }
}
