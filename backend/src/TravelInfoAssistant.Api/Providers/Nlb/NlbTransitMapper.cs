using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Nlb;

public static class NlbTransitMapper
{
    private static readonly TimeZoneInfo HongKongTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Hong_Kong");

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(
        IReadOnlyList<NlbRouteRow> rows) =>
        rows
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.RouteId) &&
                !string.IsNullOrWhiteSpace(item.RouteNumber))
            .Select(item =>
            {
                var (originZh, destinationZh) = Endpoints(item.NameZh);
                return new TransitRouteResponse(
                    $"NLB:{item.RouteId}",
                    item.RouteNumber,
                    item.RouteNumber,
                    originZh,
                    destinationZh,
                    ["新大嶼山巴士"],
                    [new TransitDirectionResponse(0, destinationZh, originZh, destinationZh)],
                    [],
                    $"NLB:{item.RouteId}");
            })
            .OrderBy(item => item.NameZh.PadLeft(8, '0'), StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.OriginName, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<TransitStopResponse> MapStops(
        IReadOnlyList<NlbStopRow> rows) =>
        rows.Select((item, index) => new TransitStopResponse(
                item.StopId,
                item.NameZh,
                item.NameEn,
                index + 1,
                0,
                ParseDouble(item.Latitude),
                ParseDouble(item.Longitude)))
            .ToList();

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string routeId,
        string routeNumber,
        string destinationName,
        string stopId,
        string stopName,
        DateTimeOffset sourceUpdatedAt,
        IReadOnlyList<NlbEtaRow> rows) =>
        rows
            .Select(item => (Row: item, EstimatedAt: ParseHongKongTime(item.EstimatedArrivalTime)))
            .Where(item => item.EstimatedAt.HasValue)
            .OrderBy(item => item.EstimatedAt)
            .Take(3)
            .Select((item, index) => new TransitArrivalResponse(
                $"NLB:{routeId}:{stopId}:{index}:{item.EstimatedAt:O}",
                "bus",
                stopId,
                stopName,
                $"NLB:{routeId}",
                routeNumber,
                null,
                null,
                destinationName,
                0,
                item.EstimatedAt,
                item.EstimatedAt,
                ParseHongKongTime(item.Row.GenerateTime) ?? sourceUpdatedAt,
                ArrivalStatus(item.Row),
                false))
            .ToList();

    public static (string Origin, string Destination) Endpoints(string routeName)
    {
        var parts = routeName.Split('>', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (routeName.Trim(), routeName.Trim());
    }

    private static double? ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;

    private static DateTimeOffset? ParseHongKongTime(string value)
    {
        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var local))
        {
            return null;
        }
        return new DateTimeOffset(local, HongKongTimeZone.GetUtcOffset(local));
    }

    private static string ArrivalStatus(NlbEtaRow row)
    {
        var status = row.GpsEquipped == 1 ? "即時預計" : "班表預計";
        return string.IsNullOrWhiteSpace(row.RouteVariantName)
            ? status
            : $"{status}・{row.RouteVariantName}";
    }
}
