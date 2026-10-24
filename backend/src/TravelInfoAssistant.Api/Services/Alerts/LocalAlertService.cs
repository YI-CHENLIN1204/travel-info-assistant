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

        var matching = providers.Where(item =>
            string.Equals(item.CountryCode, city.CountryCode, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matching.Length == 0)
        {
            return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                [],
                $"{city.NameZh}的官方在地警報尚未完成整合。",
                timeProvider);
        }

        if (matching.Length == 1) return await matching[0].GetAlertsAsync(city, cancellationToken);
        var results = await Task.WhenAll(matching.Select(async item =>
        {
            try { return await item.GetAlertsAsync(city, cancellationToken); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>.Unavailable(
                    [], "部分官方警報來源連線逾時，請查官方公告。", timeProvider);
            }
        }));
        var available = results.Where(item => item.DataStatus != "unavailable").ToArray();
        var messages = string.Join(" ", results.Select(item => item.Message)
            .Where(message => !string.IsNullOrWhiteSpace(message)));
        return new ProviderQueryResult<IReadOnlyList<LocalAlertResponse>>(
            results.SelectMany(item => item.Data).DistinctBy(item => item.Id).ToArray(),
            available.Length == 0 ? "unavailable" : available.Any(item => item.Stale) ? "cached" : "realtime",
            available.MaxBy(item => item.SourceUpdatedAt)?.SourceUpdatedAt,
            results.Min(item => item.FetchedAt), results.Any(item => item.Stale),
            string.IsNullOrWhiteSpace(messages) ? null : messages,
            string.Join("、", results.Select(item => item.Source).Distinct()));
    }
}
