using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Boca;

public sealed class BocaAlertsProvider(
    IBocaApiClient apiClient,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<BocaAlertsProvider> logger) : IBocaAlertsProvider
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(3);

    public async Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>> GetAlertsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await cache.GetOrCreateAsync<IReadOnlyList<TravelAlertResponse>>(
                "alerts:boca:rss:v2",
                FreshFor,
                RetainFor,
                async token =>
                {
                    var response = await apiClient.GetAlertsAsync(token);
                    var alerts = BocaAlertMapper.Map(response.Xml);
                    return new ProviderPayload<IReadOnlyList<TravelAlertResponse>>(
                        alerts,
                        "scheduled",
                        alerts.Max(item => item.UpdatedAt) ?? response.LastModified,
                        response.FetchedAt);
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "BOCA travel alert query failed.");
            return ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>.Unavailable(
                [],
                "外交部旅遊警示目前無法更新，請稍後再試。",
                timeProvider);
        }
    }
}
