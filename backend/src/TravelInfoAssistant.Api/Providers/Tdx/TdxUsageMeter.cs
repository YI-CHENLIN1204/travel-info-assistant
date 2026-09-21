using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public interface ITdxUsageMeter
{
    Task<bool> TryReserveRequestAsync(CancellationToken cancellationToken);
    Task AddResponseBytesAsync(long responseBytes, CancellationToken cancellationToken);
    Task<TdxProviderStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
}

public sealed class TdxUsageMeter(
    IDistributedCache distributedCache,
    IMemoryCache memoryCache,
    IOptions<TdxOptions> options,
    TimeProvider timeProvider,
    ILogger<TdxUsageMeter> logger) : ITdxUsageMeter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<bool> TryReserveRequestAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            var state = await GetStateAsync(cancellationToken);
            var projectedRequestCount = state.RequestCount + 1;
            var projectedPoints = CalculatePoints(projectedRequestCount, state.ResponseBytes);
            if (projectedPoints > options.Value.MonthlySoftLimitPoints)
            {
                return false;
            }

            await SaveStateAsync(
                state with { RequestCount = projectedRequestCount },
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

    public async Task<TdxProviderStatusResponse> GetStatusAsync(
        CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(cancellationToken);
        var settings = options.Value;
        return new TdxProviderStatusResponse(
            settings.IsConfigured,
            state.BillingCycle,
            state.RequestCount,
            state.ResponseBytes,
            Math.Round(CalculatePoints(state.RequestCount, state.ResponseBytes), 4),
            settings.MonthlySoftLimitPoints,
            settings.MonthlyHardLimitPoints,
            settings.RequestsPerMinute,
            settings.AllowPaidOverage,
            settings.PricingVerifiedAt);
    }

    private double CalculatePoints(long requestCount, long responseBytes) =>
        TdxQuotaCalculator.EstimatePoints(
            requestCount,
            responseBytes,
            options.Value.RequestsPerPoint,
            options.Value.MegabytesPerPoint);

    private async Task<TdxUsageState> GetStateAsync(CancellationToken cancellationToken)
    {
        var cycle = GetBillingCycle();
        var key = GetCacheKey(cycle);

        try
        {
            var json = await distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = JsonSerializer.Deserialize<TdxUsageState>(json, JsonOptions);
                if (state is not null)
                {
                    memoryCache.Set(key, state, TimeSpan.FromDays(35));
                    return state;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis is unavailable while reading TDX usage.");
        }

        return memoryCache.TryGetValue<TdxUsageState>(key, out var cached) && cached is not null
            ? cached
            : new TdxUsageState(cycle, 0, 0);
    }

    private async Task SaveStateAsync(
        TdxUsageState state,
        CancellationToken cancellationToken)
    {
        var key = GetCacheKey(state.BillingCycle);
        memoryCache.Set(key, state, TimeSpan.FromDays(35));

        try
        {
            await distributedCache.SetStringAsync(
                key,
                JsonSerializer.Serialize(state, JsonOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(35)
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis is unavailable while writing TDX usage.");
        }
    }

    private string GetBillingCycle() =>
        TdxTimeParser.ToTaipei(timeProvider.GetUtcNow())
            .ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string GetCacheKey(string billingCycle) => $"quota:tdx:{billingCycle}";

    private sealed record TdxUsageState(
        string BillingCycle,
        long RequestCount,
        long ResponseBytes);
}
