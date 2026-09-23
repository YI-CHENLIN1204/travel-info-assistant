using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public enum AeroDataBoxRequestKind
{
    RouteSearch,
    FlightStatus
}

public interface IAeroDataBoxUsageMeter
{
    Task<bool> TryReserveAsync(
        AeroDataBoxRequestKind kind,
        int units,
        CancellationToken cancellationToken);

    Task AddResponseBytesAsync(long responseBytes, CancellationToken cancellationToken);
    Task<AeroDataBoxProviderStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
}

public sealed class AeroDataBoxUsageMeter(
    IDistributedCache distributedCache,
    IMemoryCache memoryCache,
    IOptions<AeroDataBoxOptions> options,
    TimeProvider timeProvider,
    ILogger<AeroDataBoxUsageMeter> logger) : IAeroDataBoxUsageMeter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<bool> TryReserveAsync(
        AeroDataBoxRequestKind kind,
        int units,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await GetStateAsync(cancellationToken);
            var settings = options.Value;
            var limit = kind == AeroDataBoxRequestKind.RouteSearch
                ? settings.MonthlySoftLimitUnits
                : settings.MonthlyHardLimitUnits;

            if (state.UsedUnits + units > limit ||
                state.ResponseBytes >= settings.MonthlyTrafficSoftLimitBytes)
            {
                return false;
            }

            await SaveStateAsync(
                state with
                {
                    RequestCount = state.RequestCount + 1,
                    UsedUnits = state.UsedUnits + units
                },
                cancellationToken);
            return true;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task AddResponseBytesAsync(
        long responseBytes,
        CancellationToken cancellationToken)
    {
        if (responseBytes <= 0)
        {
            return;
        }

        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await GetStateAsync(cancellationToken);
            await SaveStateAsync(
                state with { ResponseBytes = state.ResponseBytes + responseBytes },
                cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<AeroDataBoxProviderStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(cancellationToken);
        var settings = options.Value;
        return new AeroDataBoxProviderStatusResponse(
            settings.IsConfigured,
            settings.Gateway,
            state.BillingCycle,
            state.RequestCount,
            state.UsedUnits,
            settings.MonthlySoftLimitUnits,
            settings.MonthlyHardLimitUnits,
            state.ResponseBytes,
            settings.MonthlyTrafficSoftLimitBytes,
            settings.RequestsPerSecond,
            settings.AllowPaidOverage,
            settings.PricingVerifiedAt);
    }

    private async Task<AeroDataBoxUsageState> GetStateAsync(
        CancellationToken cancellationToken)
    {
        var cycle = GetBillingCycle();
        var key = GetCacheKey(cycle);

        try
        {
            var json = await distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = JsonSerializer.Deserialize<AeroDataBoxUsageState>(json, JsonOptions);
                if (state is not null)
                {
                    memoryCache.Set(key, state, TimeSpan.FromDays(40));
                    return state;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis is unavailable while reading AeroDataBox usage.");
        }

        return memoryCache.TryGetValue<AeroDataBoxUsageState>(key, out var cached) && cached is not null
            ? cached
            : new AeroDataBoxUsageState(cycle, 0, 0, 0);
    }

    private async Task SaveStateAsync(
        AeroDataBoxUsageState state,
        CancellationToken cancellationToken)
    {
        var key = GetCacheKey(state.BillingCycle);
        memoryCache.Set(key, state, TimeSpan.FromDays(40));

        try
        {
            await distributedCache.SetStringAsync(
                key,
                JsonSerializer.Serialize(state, JsonOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(40)
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis is unavailable while writing AeroDataBox usage.");
        }
    }

    private string GetBillingCycle()
    {
        var now = timeProvider.GetUtcNow();
        var configuredDay = Math.Clamp(options.Value.BillingCycleDay, 1, 28);
        var start = new DateTimeOffset(
            now.Year,
            now.Month,
            configuredDay,
            0,
            0,
            0,
            TimeSpan.Zero);
        if (now < start)
        {
            start = start.AddMonths(-1);
        }

        return start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string GetCacheKey(string billingCycle) =>
        $"quota:aerodatabox:{billingCycle}";

    private sealed record AeroDataBoxUsageState(
        string BillingCycle,
        long RequestCount,
        long UsedUnits,
        long ResponseBytes);
}
