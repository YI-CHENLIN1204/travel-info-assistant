namespace TravelInfoAssistant.Api.Providers.Hko;

public interface IHkoApiClient
{
    Task<HkoAlertBundle> GetAlertBundleAsync(CancellationToken cancellationToken);
}

public sealed record HkoAlertBundle(
    string WarningSummaryJson,
    string WarningInfoJson,
    string SpecialWeatherTipsJson,
    string FeltEarthquakeJson,
    DateTimeOffset FetchedAt);
