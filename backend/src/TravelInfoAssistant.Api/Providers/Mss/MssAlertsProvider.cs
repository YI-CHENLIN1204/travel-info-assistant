using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Mss;

public sealed class MssAlertsProvider(
    IHttpClientFactory clients,
    IProviderCache cache,
    TimeProvider timeProvider,
    ILogger<MssAlertsProvider> logger) : ILocalAlertsProvider
{
    public string CountryCode => "SG";

    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city, CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<LocalAlertResponse>>(
                "alerts:mss:rain:v1", TimeSpan.FromMinutes(5), TimeSpan.FromHours(1),
                async token =>
                {
                    var xml = await clients.CreateClient("mss-api").GetStringAsync(
                        "https://www.weather.gov.sg/files/rss/rssHeavyRain_new.xml", token);
                    var fetched = timeProvider.GetUtcNow();
                    var alerts = MssAlertMapper.Map(xml, fetched);
                    return new ProviderPayload<IReadOnlyList<LocalAlertResponse>>(
                        alerts, "realtime", alerts.FirstOrDefault()?.UpdatedAt, fetched);
                }, cancellationToken);
            return new ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>(
                result.Data.Where(item => item.ExpiresAt > timeProvider.GetUtcNow()).ToArray(),
                result.DataStatus, result.SourceUpdatedAt, result.FetchedAt, result.Stale,
                "目前涵蓋 MSS 豪雨警告；閃電、水浸、煙霾及其他災害尚未整合，請另查官方資訊。",
                "MSS");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "MSS local warning query failed.");
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [], "新加坡官方豪雨警告目前無法更新；請查閱 MSS 官方警告。",
                timeProvider, "MSS");
        }
    }
}
