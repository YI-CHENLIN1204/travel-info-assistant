using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Ncdr;

public sealed class NcdrAlertsProvider(
    INcdrApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<NcdrAlertsProvider> logger) : ILocalAlertsProvider
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetainFor = TimeSpan.FromHours(6);
    private static readonly IReadOnlyDictionary<string, string> CountyByCityCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["taipei"] = "臺北市"
        };

    public string CountryCode => "TW";

    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city,
        CancellationToken cancellationToken)
    {
        if (!CountyByCityCode.TryGetValue(city.Code, out var county))
        {
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "這個台灣城市尚未設定 NCDR 示警區域。",
                timeProvider);
        }

        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<LocalAlertResponse>>(
                $"alerts:ncdr:active:{city.Code}:v1",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetActiveAlertsAsync(county, token);
                    var alerts = NcdrAlertMapper.Map(response.Xml, county, response.FetchedAt);
                    return new ProviderPayload<IReadOnlyList<LocalAlertResponse>>(
                        alerts,
                        "realtime",
                        alerts.MaxBy(alert => alert.UpdatedAt)?.UpdatedAt,
                        response.FetchedAt);
                },
                cancellationToken);

            return FilterExpired(result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "NCDR local alert query failed for {CityCode}.", city.Code);
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                "NCDR 在地警報目前無法更新，請稍後再試。",
                timeProvider);
        }
    }

    private ProviderQueryResult<IReadOnlyList<LocalAlertResponse>> FilterExpired(
        ProviderQueryResult<IReadOnlyList<LocalAlertResponse>> result)
    {
        var now = timeProvider.GetUtcNow();
        var active = result.Data
            .Where(alert => !alert.ExpiresAt.HasValue || alert.ExpiresAt.Value >= now)
            .ToArray();
        return new ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>(
            active,
            result.DataStatus,
            result.SourceUpdatedAt,
            result.FetchedAt,
            result.Stale,
            result.Message);
    }
}
