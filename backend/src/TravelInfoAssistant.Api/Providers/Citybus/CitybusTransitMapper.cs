using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Citybus;

public static class CitybusTransitMapper
{
    public static IReadOnlyList<TransitRouteResponse> MapRoutes(
        IReadOnlyList<CitybusRouteRow> rows) =>
        rows
            .Where(item => !string.IsNullOrWhiteSpace(item.Route))
            .GroupBy(item => item.Route, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var route = group.First();
                return new TransitRouteResponse(
                    $"CTB:{route.Route}",
                    route.Route,
                    route.Route,
                    route.OriginZh,
                    route.DestinationZh,
                    ["城巴"],
                    [
                        new TransitDirectionResponse(0, route.DestinationZh, route.OriginZh, route.DestinationZh),
                        new TransitDirectionResponse(1, route.OriginZh, route.DestinationZh, route.OriginZh)
                    ],
                    [],
                    $"CTB:{route.Route}");
            })
            .OrderBy(item => item.NameZh.PadLeft(8, '0'), StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static IReadOnlyList<TransitStopResponse> MapStops(
        string route,
        int direction,
        IReadOnlyList<CitybusRouteStopRow> routeStops,
        IReadOnlyList<CitybusStopRow> stops)
    {
        var stopLookup = stops.ToDictionary(item => item.StopId, StringComparer.OrdinalIgnoreCase);
        var bound = direction == 0 ? "O" : "I";
        return routeStops
            .Where(item =>
                item.Route.Equals(route, StringComparison.OrdinalIgnoreCase) &&
                item.Direction.Equals(bound, StringComparison.OrdinalIgnoreCase) &&
                stopLookup.ContainsKey(item.StopId))
            .OrderBy(item => item.Sequence)
            .Select(item =>
            {
                var stop = stopLookup[item.StopId];
                return new TransitStopResponse(
                    stop.StopId,
                    stop.NameZh,
                    stop.NameEn,
                    item.Sequence,
                    direction,
                    ParseDouble(stop.Latitude),
                    ParseDouble(stop.Longitude));
            })
            .ToList();
    }

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string route,
        int direction,
        string stopId,
        string stopName,
        IReadOnlyList<CitybusEtaRow> rows)
    {
        var bound = direction == 0 ? "O" : "I";
        return rows
            .Where(item =>
                item.Route.Equals(route, StringComparison.OrdinalIgnoreCase) &&
                item.Direction.Equals(bound, StringComparison.OrdinalIgnoreCase) &&
                item.EstimatedAt.HasValue)
            .OrderBy(item => item.EstimatedAt)
            .Take(3)
            .Select(item => new TransitArrivalResponse(
                $"CTB:{route}:{bound}:{stopId}:{item.EtaSequence}:{item.EstimatedAt:O}",
                "bus",
                stopId,
                stopName,
                $"CTB:{route}",
                route,
                null,
                null,
                item.DestinationZh,
                direction,
                item.EstimatedAt,
                item.EstimatedAt,
                item.DataTimestamp,
                string.IsNullOrWhiteSpace(item.RemarkZh) ? "即時預估" : item.RemarkZh,
                false))
            .ToList();
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
}
