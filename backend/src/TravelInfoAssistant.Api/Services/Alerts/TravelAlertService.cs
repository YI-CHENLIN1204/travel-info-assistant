using Microsoft.EntityFrameworkCore;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Infrastructure;
using TravelInfoAssistant.Api.Providers.Boca;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Services.Alerts;

public sealed class TravelAlertService(
    AppDbContext dbContext,
    IBocaAlertsProvider provider,
    TimeProvider timeProvider) : ITravelAlertService
{
    private static readonly string[] PopularCountryCodes = ["JP", "KR", "TH", "US", "FR"];

    public async Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>?> GetCityAlertsAsync(
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

        if (string.Equals(city.CountryCode, "TW", StringComparison.OrdinalIgnoreCase))
        {
            return new ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>(
                [],
                "scheduled",
                null,
                timeProvider.GetUtcNow(),
                false,
                "台灣不屬於外交部國外旅遊警示適用地區；出國目的地請參考下方熱門地區。 ");
        }

        var result = await provider.GetAlertsAsync(cancellationToken);
        return Filter(result, alert =>
            string.Equals(alert.CountryCode, city.CountryCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ProviderQueryResult<IReadOnlyList<TravelAlertResponse>>> GetPopularAlertsAsync(
        CancellationToken cancellationToken)
    {
        var result = await provider.GetAlertsAsync(cancellationToken);
        var alerts = PopularCountryCodes
            .Select(code => result.Data
                .Where(alert => string.Equals(
                    alert.CountryCode,
                    code,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(alert => alert.Level)
                .ThenByDescending(alert => alert.UpdatedAt)
                .FirstOrDefault())
            .Where(alert => alert is not null)
            .Cast<TravelAlertResponse>()
            .ToArray();

        return Copy(result, alerts);
    }

    private static ProviderQueryResult<IReadOnlyList<TravelAlertResponse>> Filter(
        ProviderQueryResult<IReadOnlyList<TravelAlertResponse>> result,
        Func<TravelAlertResponse, bool> predicate) =>
        Copy(result, result.Data.Where(predicate).ToArray());

    private static ProviderQueryResult<IReadOnlyList<TravelAlertResponse>> Copy(
        ProviderQueryResult<IReadOnlyList<TravelAlertResponse>> result,
        IReadOnlyList<TravelAlertResponse> data) =>
        new(
            data,
            result.DataStatus,
            result.SourceUpdatedAt,
            result.FetchedAt,
            result.Stale,
            result.Message);
}
