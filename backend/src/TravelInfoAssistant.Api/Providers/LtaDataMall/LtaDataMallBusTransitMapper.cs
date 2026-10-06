using System.Globalization;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.LtaDataMall;

public static class LtaDataMallBusTransitMapper
{
    private const string Prefix = "LTA-BUS";
    private static readonly TimeZoneInfo SingaporeTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(LtaBusNetwork network)
    {
        var stops = StopLookup(network);
        return network.Services
            .Where(item =>
                !string.IsNullOrWhiteSpace(item.ServiceNo) &&
                !string.IsNullOrWhiteSpace(item.Operator) &&
                item.Direction is 1 or 2)
            .GroupBy(
                item => RouteQueryId(item.Operator, item.ServiceNo),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => MapRoute(group.ToList(), network, stops))
            .Where(item => item is not null)
            .Cast<TransitRouteResponse>()
            .OrderBy(item => RouteNumber(item.NameZh))
            .ThenBy(item => item.NameZh, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Operators.FirstOrDefault(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<TransitStopResponse> MapStops(
        string routeQueryId,
        int direction,
        LtaBusNetwork network)
    {
        if (!TryParseRouteQueryId(routeQueryId, out var operatorCode, out var serviceNo))
        {
            return [];
        }

        var sourceDirection = direction + 1;
        var stops = StopLookup(network);
        return network.Routes
            .Where(item =>
                item.Operator.Equals(operatorCode, StringComparison.OrdinalIgnoreCase) &&
                item.ServiceNo.Equals(serviceNo, StringComparison.OrdinalIgnoreCase) &&
                item.Direction == sourceDirection &&
                stops.ContainsKey(item.BusStopCode))
            .OrderBy(item => item.StopSequence)
            .Select(item =>
            {
                var stop = stops[item.BusStopCode];
                return new TransitStopResponse(
                    stop.BusStopCode,
                    StopName(stop),
                    StopName(stop),
                    item.StopSequence,
                    direction,
                    stop.Latitude,
                    stop.Longitude);
            })
            .ToList();
    }

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string routeQueryId,
        int direction,
        string stopId,
        LtaBusArrivalResponse response,
        LtaBusNetwork network,
        DateTimeOffset fetchedAt)
    {
        if (!TryParseRouteQueryId(routeQueryId, out var operatorCode, out var serviceNo))
        {
            return [];
        }

        var stops = StopLookup(network);
        var stopName = stops.TryGetValue(stopId, out var stop) ? StopName(stop) : stopId;
        var selectedDirection = network.Services.FirstOrDefault(item =>
            item.Operator.Equals(operatorCode, StringComparison.OrdinalIgnoreCase) &&
            item.ServiceNo.Equals(serviceNo, StringComparison.OrdinalIgnoreCase) &&
            item.Direction == direction + 1);
        var fallbackDestination = selectedDirection is null
            ? null
            : StopName(selectedDirection.DestinationCode, stops);
        var service = response.Services.FirstOrDefault(item =>
            item.Operator.Equals(operatorCode, StringComparison.OrdinalIgnoreCase) &&
            item.ServiceNo.Equals(serviceNo, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return [];
        }

        return new[] { service.NextBus, service.NextBus2, service.NextBus3 }
            .Select((item, index) => MapArrival(
                item,
                index,
                routeQueryId,
                serviceNo,
                direction,
                stopId,
                stopName,
                fallbackDestination,
                stops,
                fetchedAt))
            .Where(item => item is not null)
            .Cast<TransitArrivalResponse>()
            .OrderBy(item => item.EstimatedAt ?? item.ScheduledAt)
            .Take(3)
            .ToList();
    }

    public static DateTimeOffset? GetEndedServiceDayLastDeparture(
        string routeQueryId,
        int direction,
        string stopId,
        LtaBusNetwork network,
        DateTimeOffset now)
    {
        if (!TryParseRouteQueryId(routeQueryId, out var operatorCode, out var serviceNo))
        {
            return null;
        }

        var localNow = TimeZoneInfo.ConvertTime(now, SingaporeTimeZone);
        var serviceDate = DateOnly.FromDateTime(localNow.DateTime);
        if (localNow.Hour < 4)
        {
            serviceDate = serviceDate.AddDays(-1);
        }

        var route = network.Routes.FirstOrDefault(item =>
            item.Operator.Equals(operatorCode, StringComparison.OrdinalIgnoreCase) &&
            item.ServiceNo.Equals(serviceNo, StringComparison.OrdinalIgnoreCase) &&
            item.Direction == direction + 1 &&
            item.BusStopCode.Equals(stopId, StringComparison.OrdinalIgnoreCase));
        if (route is null)
        {
            return null;
        }

        var firstText = ServiceTime(route, serviceDate.DayOfWeek, first: true);
        var lastText = ServiceTime(route, serviceDate.DayOfWeek, first: false);
        if (!TryParseServiceTime(serviceDate, firstText, out var firstDeparture) ||
            !TryParseServiceTime(serviceDate, lastText, out var lastDeparture))
        {
            return null;
        }

        if (lastDeparture <= firstDeparture)
        {
            lastDeparture = lastDeparture.AddDays(1);
        }

        return lastDeparture < now ? lastDeparture : null;
    }

    public static string RouteQueryId(string operatorCode, string serviceNo) =>
        $"{Prefix}:{operatorCode}:{serviceNo}";

    public static bool TryParseRouteQueryId(
        string value,
        out string operatorCode,
        out string serviceNo)
    {
        operatorCode = string.Empty;
        serviceNo = string.Empty;
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 3 ||
            !parts[0].Equals(Prefix, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[1]) ||
            string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        operatorCode = parts[1];
        serviceNo = parts[2];
        return true;
    }

    private static TransitRouteResponse? MapRoute(
        IReadOnlyList<LtaBusServiceRow> services,
        LtaBusNetwork network,
        IReadOnlyDictionary<string, LtaBusStopRow> stops)
    {
        var first = services.First();
        var directions = services
            .GroupBy(item => item.Direction)
            .Select(group => group.First())
            .OrderBy(item => item.Direction)
            .Select(item => new TransitDirectionResponse(
                item.Direction - 1,
                StopName(item.DestinationCode, stops),
                StopName(item.OriginCode, stops),
                StopName(item.DestinationCode, stops)))
            .ToList();
        if (directions.Count == 0)
        {
            return null;
        }

        var stationNames = network.Routes
            .Where(item =>
                item.Operator.Equals(first.Operator, StringComparison.OrdinalIgnoreCase) &&
                item.ServiceNo.Equals(first.ServiceNo, StringComparison.OrdinalIgnoreCase) &&
                item.Direction == services.Min(service => service.Direction))
            .OrderBy(item => item.StopSequence)
            .Select(item => StopName(item.BusStopCode, stops))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
        if (stationNames.Count == 0)
        {
            return null;
        }

        var queryId = RouteQueryId(first.Operator, first.ServiceNo);
        return new TransitRouteResponse(
            queryId,
            first.ServiceNo,
            first.ServiceNo,
            directions[0].OriginName,
            directions[0].DestinationName,
            [OperatorName(first.Operator)],
            directions,
            stationNames,
            queryId);
    }

    private static TransitArrivalResponse? MapArrival(
        LtaBusArrivalEstimate estimate,
        int index,
        string routeQueryId,
        string serviceNo,
        int direction,
        string stopId,
        string stopName,
        string? fallbackDestination,
        IReadOnlyDictionary<string, LtaBusStopRow> stops,
        DateTimeOffset fetchedAt)
    {
        if (!DateTimeOffset.TryParse(
                estimate.EstimatedArrival,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var arrivalAt))
        {
            return null;
        }

        var isRealtime = estimate.Monitored == 1;
        var destination = StopName(estimate.DestinationCode, stops) ?? fallbackDestination;
        return new TransitArrivalResponse(
            $"{routeQueryId}:{stopId}:{index}:{arrivalAt.ToUnixTimeSeconds()}",
            "bus",
            stopId,
            stopName,
            routeQueryId,
            serviceNo,
            null,
            null,
            destination,
            direction,
            arrivalAt,
            isRealtime ? arrivalAt : null,
            isRealtime ? fetchedAt : null,
            isRealtime ? "即時預估" : "表定時間",
            false);
    }

    private static IReadOnlyDictionary<string, LtaBusStopRow> StopLookup(LtaBusNetwork network) =>
        network.Stops
            .Where(item => !string.IsNullOrWhiteSpace(item.BusStopCode))
            .GroupBy(item => item.BusStopCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

    private static string StopName(LtaBusStopRow stop) =>
        string.IsNullOrWhiteSpace(stop.Description) ? stop.RoadName : stop.Description;

    private static string? StopName(
        string stopCode,
        IReadOnlyDictionary<string, LtaBusStopRow> stops) =>
        stops.TryGetValue(stopCode, out var stop) ? StopName(stop) :
        string.IsNullOrWhiteSpace(stopCode) ? null : stopCode;

    private static string OperatorName(string value) => value.ToUpperInvariant() switch
    {
        "SBST" => "SBS Transit",
        "SMRT" => "SMRT Buses",
        "TTS" => "Tower Transit Singapore",
        "GAS" => "Go-Ahead Singapore",
        _ => value
    };

    private static int RouteNumber(string value)
    {
        var digits = new string(value.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : int.MaxValue;
    }

    private static string ServiceTime(LtaBusRouteRow route, DayOfWeek day, bool first) =>
        day switch
        {
            DayOfWeek.Saturday => first ? route.SAT_FirstBus : route.SAT_LastBus,
            DayOfWeek.Sunday => first ? route.SUN_FirstBus : route.SUN_LastBus,
            _ => first ? route.WD_FirstBus : route.WD_LastBus
        };

    private static bool TryParseServiceTime(
        DateOnly serviceDate,
        string? value,
        out DateTimeOffset result)
    {
        result = default;
        var text = value?.Trim();
        if (text is null || text.Length != 4 ||
            !int.TryParse(text[..2], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(text[2..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            hours is < 0 or > 23 || minutes is < 0 or > 59)
        {
            return false;
        }

        var local = serviceDate.ToDateTime(new TimeOnly(hours, minutes), DateTimeKind.Unspecified);
        result = new DateTimeOffset(local, SingaporeTimeZone.GetUtcOffset(local));
        return true;
    }
}
