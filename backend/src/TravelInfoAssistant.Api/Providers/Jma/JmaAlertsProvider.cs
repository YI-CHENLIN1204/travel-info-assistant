using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Jma;

public sealed class JmaAlertsProvider(
    IJmaApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<JmaAlertsProvider> logger) : ILocalAlertsProvider
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetainFor = TimeSpan.FromHours(6);

    public string CountryCode => "JP";

    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(city.Code, "tokyo", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "這個日本城市尚未設定 JMA 警報區域。",
                timeProvider,
                "JMA");
        }

        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<LocalAlertResponse>>(
                "alerts:jma:tokyo:v1",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var bundle = await apiClient.GetTokyoAlertDocumentsAsync(token);
                    var alerts = JmaAlertMapper.Map(bundle, "東京都");
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
            logger.LogWarning(exception, "JMA local alert query failed for {CityCode}.", city.Code);
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "日本氣象廳在地警報目前無法更新，請稍後再試。",
                timeProvider,
                "JMA");
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
            "JMA");
    }
}
