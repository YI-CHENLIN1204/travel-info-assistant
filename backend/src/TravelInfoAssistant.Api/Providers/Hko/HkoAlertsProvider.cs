using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Hko;

public sealed class HkoAlertsProvider(
    IHkoApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<HkoAlertsProvider> logger) : ILocalAlertsProvider
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetainFor = TimeSpan.FromHours(6);

    public string CountryCode => "HK";

    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(city.Code, "hong-kong", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "這個香港城市尚未設定 HKO 警報區域。",
                timeProvider,
                "HKO");
        }

        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<LocalAlertResponse>>(
                "alerts:hko:hong-kong:v1",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var bundle = await apiClient.GetAlertBundleAsync(token);
                    var alerts = HkoAlertMapper.Map(bundle);
                    return new ProviderPayload<IReadOnlyList<LocalAlertResponse>>(
                        alerts,
                        "realtime",
                        alerts.MaxBy(alert => alert.UpdatedAt)?.UpdatedAt,
                        bundle.FetchedAt);
                },
                cancellationToken);
            return FilterExpired(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "HKO local alert query failed for {CityCode}.", city.Code);
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "香港天文台在地警報目前無法更新，請稍後再試。",
                timeProvider,
                "HKO");
        }
    }

    private ProviderQueryResult<IReadOnlyList<LocalAlertResponse>> FilterExpired(
        ProviderQueryResult<IReadOnlyList<LocalAlertResponse>> result)
    {
        var now = timeProvider.GetUtcNow();
        var current = result.Data
            .Where(alert => !alert.ExpiresAt.HasValue || alert.ExpiresAt.Value >= now)
            .ToArray();
        return new ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>(
            current,
            result.DataStatus,
            result.SourceUpdatedAt,
            result.FetchedAt,
            result.Stale,
            result.Message,
            "HKO");
    }
}
