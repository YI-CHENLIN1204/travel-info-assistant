using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.Citybus;
using TravelInfoAssistant.Api.Providers.Kmb;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Providers.HongKong;

public interface IHongKongBusTransitProvider
{
    Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string queryId,
        int direction,
        CancellationToken cancellationToken);
    Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string queryId,
        int direction,
        string stopId,
        CancellationToken cancellationToken);
}

public sealed class HongKongBusTransitProvider(
    IKmbTransitProvider kmbProvider,
    ICitybusTransitProvider citybusProvider,
    TimeProvider timeProvider) : IHongKongBusTransitProvider
{
    private const string Source = "香港巴士開放數據";

    public async Task<ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>> GetBusRoutesAsync(
        CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(
            kmbProvider.GetBusRoutesAsync(cancellationToken),
            citybusProvider.GetBusRoutesAsync(cancellationToken));
        var data = results
            .SelectMany(item => item.Data)
            .OrderBy(item => item.NameZh.PadLeft(8, '0'), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Operators.FirstOrDefault(), StringComparer.Ordinal)
            .ToList();
        var failures = results
            .Where(item => item.DataStatus == "unavailable")
            .Select(item => item.Message)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        var successful = results.Where(item => item.DataStatus != "unavailable").ToList();
        if (successful.Count == 0)
        {
            return ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>.Unavailable(
                [],
                string.Join(' ', failures),
                timeProvider,
                Source);
        }

        return new ProviderQueryResult<IReadOnlyList<TransitRouteResponse>>(
            data,
            successful.Any(item => item.DataStatus == "cached") ? "cached" : "scheduled",
            Latest(successful.Select(item => item.SourceUpdatedAt)),
            successful.Max(item => item.FetchedAt),
            successful.Any(item => item.Stale),
            failures.Count > 0 ? string.Join(' ', failures) : null,
            Source);
    }

    public Task<ProviderQueryResult<IReadOnlyList<TransitStopResponse>>> GetBusStopsAsync(
        string queryId,
        int direction,
        CancellationToken cancellationToken)
    {
        var (provider, route) = ParseQueryId(queryId);
        return provider switch
        {
            "CTB" => citybusProvider.GetBusStopsAsync(route, direction, cancellationToken),
            "KMB" => kmbProvider.GetBusStopsAsync(route, direction, cancellationToken),
            _ => Task.FromResult(Unavailable<TransitStopResponse>())
        };
    }

    public Task<ProviderQueryResult<IReadOnlyList<TransitArrivalResponse>>> GetBusArrivalsAsync(
        string queryId,
        int direction,
        string stopId,
        CancellationToken cancellationToken)
    {
        var (provider, route) = ParseQueryId(queryId);
        return provider switch
        {
            "CTB" => citybusProvider.GetBusArrivalsAsync(route, direction, stopId, cancellationToken),
            "KMB" => kmbProvider.GetBusArrivalsAsync(route, direction, stopId, cancellationToken),
            _ => Task.FromResult(Unavailable<TransitArrivalResponse>())
        };
    }

    private ProviderQueryResult<IReadOnlyList<T>> Unavailable<T>() =>
        ProviderQueryResult<IReadOnlyList<T>>.Unavailable(
            [],
            "無法辨識香港巴士營運業者，請重新選擇路線。",
            timeProvider,
            Source);

    private static (string Provider, string Route) ParseQueryId(string value)
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
        {
            return ("KMB", value);
        }
        return (value[..separator].ToUpperInvariant(), value[(separator + 1)..]);
    }

    private static DateTimeOffset? Latest(IEnumerable<DateTimeOffset?> values) =>
        values.Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty().Max() is var value &&
        value != default
            ? value
            : null;
}
