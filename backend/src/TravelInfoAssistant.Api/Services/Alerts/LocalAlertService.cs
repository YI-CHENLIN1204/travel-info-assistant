using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Infrastructure;
using TravelInfoAssistant.Api.Providers.LocalAlerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Alerts;

public sealed class LocalAlertService(
    AppDbContext dbContext,
    IEnumerable<ILocalAlertsProvider> providers,
    TimeProvider timeProvider) : ILocalAlertService
{
    public async Task<ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>?> GetCityAlertsAsync(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await dbContext.Cities
            .AsNoTracking()
            .Where(item => item.IsActive)
            .FirstOrDefaultAsync(item => item.Id == cityId, cancellationToken);
        if (city is null)
        {
            return null;
        }

        var provider = providers.FirstOrDefault(item =>
            string.Equals(item.CountryCode, city.CountryCode, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                $"{city.NameZh}的官方在地警報尚未完成整合。",
                timeProvider);
        }

        return await provider.GetAlertsAsync(city, cancellationToken);
    }
}
