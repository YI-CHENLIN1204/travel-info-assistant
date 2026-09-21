using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace TravelInfoAssistant.Api.Services.Transit;

public sealed record ProviderPayload<T>(
    T Data,
    string DataStatus,
    DateTimeOffset? SourceUpdatedAt,
    DateTimeOffset FetchedAt);

public interface IProviderCache
{
    Task<ProviderQueryResult<T>> GetOrCreateAsync<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<ProviderPayload<T>>> factory,
        CancellationToken cancellationToken);
}

public sealed class ProviderCache(
    IDistributedCache distributedCache,
    IMemoryCache memoryCache,
    TimeProvider timeProvider,
    ILogger<ProviderCache> logger) : IProviderCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<ProviderQueryResult<T>> GetOrCreateAsync<T>(
        string key,
        TimeSpan freshFor,
        TimeSpan retainFor,
        Func<CancellationToken, Task<ProviderPayload<T>>> factory,
        CancellationToken cancellationToken)
    {
        var cached = await ReadAsync<T>(key, cancellationToken);
        if (IsFresh(cached, freshFor))
        {
            return FromCache(cached!, false, null);
        }

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await ReadAsync<T>(key, cancellationToken);
            if (IsFresh(cached, freshFor))
            {
                return FromCache(cached!, false, null);
            }

            try
            {
                var fresh = await factory(cancellationToken);
                var envelope = new CacheEnvelope<T>(
                    fresh.Data,
                    fresh.DataStatus,
                    fresh.SourceUpdatedAt,
                    fresh.FetchedAt);
                await WriteAsync(key, envelope, retainFor, cancellationToken);

                return new ProviderQueryResult<T>(
                    fresh.Data,
                    fresh.DataStatus,
                    fresh.SourceUpdatedAt,
                    fresh.FetchedAt,
                    false,
                    null);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException && cached is not null)
            {
                logger.LogWarning(
                    exception,
                    "Provider refresh failed for {CacheKey}; serving retained data.",
                    key);
                return FromCache(
                    cached,
                    true,
                    "資料來源暫時無法更新，目前顯示上次成功取得的資料。");
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private bool IsFresh<T>(CacheEnvelope<T>? envelope, TimeSpan freshFor) =>
        envelope is not null && timeProvider.GetUtcNow() - envelope.FetchedAt <= freshFor;

    private static ProviderQueryResult<T> FromCache<T>(
        CacheEnvelope<T> envelope,
        bool stale,
        string? message) =>
        new(
            envelope.Data,
            "cached",
            envelope.SourceUpdatedAt,
            envelope.FetchedAt,
            stale,
            message);

    private async Task<CacheEnvelope<T>?> ReadAsync<T>(
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var value = JsonSerializer.Deserialize<CacheEnvelope<T>>(json, JsonOptions);
                if (value is not null)
                {
                    memoryCache.Set(key, value, TimeSpan.FromMinutes(30));
                    return value;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis read failed for {CacheKey}.", key);
        }

        return memoryCache.TryGetValue<CacheEnvelope<T>>(key, out var cached)
            ? cached
            : null;
    }

    private async Task WriteAsync<T>(
        string key,
        CacheEnvelope<T> envelope,
        TimeSpan retainFor,
        CancellationToken cancellationToken)
    {
        memoryCache.Set(key, envelope, retainFor);

        try
        {
            await distributedCache.SetStringAsync(
                key,
                JsonSerializer.Serialize(envelope, JsonOptions),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = retainFor
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Redis write failed for {CacheKey}.", key);
        }
    }

    private sealed record CacheEnvelope<T>(
        T Data,
        string DataStatus,
        DateTimeOffset? SourceUpdatedAt,
        DateTimeOffset FetchedAt);
}
