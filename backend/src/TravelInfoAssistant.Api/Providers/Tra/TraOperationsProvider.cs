using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Domain;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.Tra;

public sealed class TraOperationsProvider(IHttpClientFactory clients, IProviderCache cache,
    TimeProvider timeProvider, ILogger<TraOperationsProvider> logger) : ILocalAlertsProvider
{
    public string CountryCode => "TW";

    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>> GetAlertsAsync(
        City city, CancellationToken cancellationToken)
    {
        try
        {
            var result = await cache.GetOrCreateAsync<IReadOnlyList<LocalAlertResponse>>(
                "alerts:tra:operations:v1", TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
                async token =>
                {
                    var html = await clients.CreateClient("tra-operations")
                        .GetStringAsync(TraOperationsMapper.SourceUrl, token);
                    var alerts = TraOperationsMapper.Map(html);
                    return new ProviderPayload<IReadOnlyList<LocalAlertResponse>>(alerts,
                        "realtime", alerts.FirstOrDefault()?.UpdatedAt, timeProvider.GetUtcNow());
                }, cancellationToken);
            return result with { Source = "台鐵官方運行公告",
                Message = result.Stale ? "台鐵公告暫時無法更新，顯示十五分鐘內的備援資料；請查官方運行狀態。" : null };
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "TRA operations query failed.");
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable([],
                "台鐵運行公告目前無法更新，請查官方運行狀態；不代表所有路線正常。",
                timeProvider, "台鐵官方運行公告");
        }
    }
}
