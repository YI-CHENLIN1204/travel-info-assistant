using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Mtr;

public static class MtrTransitMapper
{
    private static readonly IReadOnlyDictionary<string, (string Zh, string En)> LineNames =
        new Dictionary<string, (string Zh, string En)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AEL"] = ("機場快綫", "Airport Express"),
            ["TCL"] = ("東涌綫", "Tung Chung Line"),
            ["TML"] = ("屯馬綫", "Tuen Ma Line"),
            ["TKL"] = ("將軍澳綫", "Tseung Kwan O Line"),
            ["EAL"] = ("東鐵綫", "East Rail Line"),
            ["SIL"] = ("南港島綫", "South Island Line"),
            ["TWL"] = ("荃灣綫", "Tsuen Wan Line"),
            ["ISL"] = ("港島綫", "Island Line"),
            ["KTL"] = ("觀塘綫", "Kwun Tong Line"),
            ["DRL"] = ("迪士尼綫", "Disneyland Resort Line")
        };

    public static IReadOnlyList<TransitRouteResponse> MapRoutes(
        IReadOnlyList<MtrStationRow> rows) =>
        rows
            .Where(row => LineNames.ContainsKey(row.LineCode))
            .GroupBy(row => row.LineCode, StringComparer.OrdinalIgnoreCase)
            .Select(MapRoute)
            .OrderBy(route => route.NameZh, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<MetroStationResponse> MapStations(
        IReadOnlyList<MtrStationRow> rows) =>
        rows
            .Where(row => LineNames.ContainsKey(row.LineCode))
            .GroupBy(
                row => $"{row.LineCode}:{row.StationCode}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(row => new MetroStationResponse(
                StationId(row.LineCode, row.StationCode),
                row.NameZh,
                row.NameEn,
                null,
                null,
                null,
                row.StationCode,
                RouteId(row.LineCode),
                LineNames[row.LineCode].Zh))
            .OrderBy(station => station.RailwayName, StringComparer.Ordinal)
            .ThenBy(station => station.NameZh, StringComparer.Ordinal)
            .ToList();

    public static IReadOnlyList<TransitArrivalResponse> MapArrivals(
        string lineCode,
        string stationCode,
        MtrScheduleResponse response,
        IReadOnlyList<MtrStationRow> rows)
    {
        var key = $"{lineCode}-{stationCode}";
        if (!response.Data.TryGetValue(key, out var schedule))
        {
            return [];
        }

        var station = rows.FirstOrDefault(row =>
            row.LineCode.Equals(lineCode, StringComparison.OrdinalIgnoreCase) &&
            row.StationCode.Equals(stationCode, StringComparison.OrdinalIgnoreCase));
        if (station is null || !LineNames.TryGetValue(lineCode, out var lineName))
        {
            return [];
        }

        var sourceUpdatedAt = MtrApiClient.ParseHongKongTime(
            schedule.SystemTime ?? response.SystemTime);
        var serviceStatus = response.IsDelay?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true
            ? "列車延誤"
            : "即時預估";

        return schedule.Up
            .Select(item => MapArrival(
                item,
                0,
                "UP",
                lineCode,
                station,
                lineName,
                sourceUpdatedAt,
                serviceStatus,
                rows))
            .Concat(schedule.Down.Select(item => MapArrival(
                item,
                1,
                "DOWN",
                lineCode,
                station,
                lineName,
                sourceUpdatedAt,
                serviceStatus,
                rows)))
            .Where(item => item is not null)
            .Cast<TransitArrivalResponse>()
            .OrderBy(item => item.EstimatedAt)
            .ToList();
    }

    public static MetroServiceStatusResponse? MapStatus(
        string lineCode,
        MtrScheduleResponse response)
    {
        if (!LineNames.TryGetValue(lineCode, out var lineName))
        {
            return null;
        }

        var updatedAt = MtrApiClient.ParseHongKongTime(response.SystemTime);
        var delayed = response.IsDelay?.Equals("Y", StringComparison.OrdinalIgnoreCase) == true;
        var message = response.Status == 1
            ? delayed ? "列車服務受延誤，請預留額外乘車時間。" : "列車服務正常。"
            : string.IsNullOrWhiteSpace(response.Message)
                ? "港鐵目前發布了服務警示。"
                : response.Message.Trim();

        return new MetroServiceStatusResponse(
            $"MTR:Status:{lineCode}",
            RouteId(lineCode),
            lineName.Zh,
            null,
            null,
            updatedAt,
            updatedAt?.AddSeconds(45),
            message);
    }

    public static bool TryParseStationId(
        string stationId,
        out string lineCode,
        out string stationCode)
    {
        var parts = stationId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 && parts[0].Equals("MTR", StringComparison.OrdinalIgnoreCase))
        {
            lineCode = parts[1].ToUpperInvariant();
            stationCode = parts[2].ToUpperInvariant();
            return LineNames.ContainsKey(lineCode);
        }

        lineCode = string.Empty;
        stationCode = string.Empty;
        return false;
    }

    public static bool TryParseRouteId(string routeId, out string lineCode)
    {
        var parts = routeId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[0].Equals("MTR", StringComparison.OrdinalIgnoreCase))
        {
            lineCode = parts[1].ToUpperInvariant();
            return LineNames.ContainsKey(lineCode);
        }

        lineCode = string.Empty;
        return false;
    }

    private static TransitRouteResponse MapRoute(IGrouping<string, MtrStationRow> group)
    {
        var primaryDirection = group
            .GroupBy(row => row.Direction, StringComparer.OrdinalIgnoreCase)
            .Select(direction => direction
                .GroupBy(row => row.StationCode, StringComparer.OrdinalIgnoreCase)
                .Select(stations => stations.First())
                .OrderBy(row => row.Sequence)
                .ToList())
            .OrderByDescending(direction => direction.Count)
            .First();
        var stationNames = primaryDirection.Select(row => row.NameZh).ToList();
        stationNames.AddRange(group
            .Where(row => !stationNames.Contains(row.NameZh, StringComparer.OrdinalIgnoreCase))
            .Select(row => row.NameZh)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        var origin = primaryDirection.First().NameZh;
        var destination = primaryDirection.Last().NameZh;
        var names = LineNames[group.Key];

        return new TransitRouteResponse(
            RouteId(group.Key),
            names.Zh,
            names.En,
            origin,
            destination,
            ["港鐵"],
            [
                new TransitDirectionResponse(0, destination, origin, destination),
                new TransitDirectionResponse(1, origin, destination, origin)
            ],
            stationNames);
    }

    private static TransitArrivalResponse? MapArrival(
        MtrTrainPrediction prediction,
        int direction,
        string directionKey,
        string lineCode,
        MtrStationRow station,
        (string Zh, string En) lineName,
        DateTimeOffset? sourceUpdatedAt,
        string serviceStatus,
        IReadOnlyList<MtrStationRow> rows)
    {
        var estimatedAt = MtrApiClient.ParseHongKongTime(prediction.Time);
        if (prediction.Valid?.Equals("Y", StringComparison.OrdinalIgnoreCase) != true ||
            !estimatedAt.HasValue)
        {
            return null;
        }

        var destination = rows.FirstOrDefault(row =>
            row.StationCode.Equals(
                prediction.DestinationCode,
                StringComparison.OrdinalIgnoreCase));
        return new TransitArrivalResponse(
            $"MTR:{lineCode}:{station.StationCode}:{directionKey}:{prediction.Sequence}:{prediction.Time}",
            "metro",
            StationId(lineCode, station.StationCode),
            station.NameZh,
            RouteId(lineCode),
            lineName.Zh,
            RouteId(lineCode),
            lineName.Zh,
            destination?.NameZh ?? prediction.DestinationCode,
            direction,
            null,
            estimatedAt,
            sourceUpdatedAt,
            serviceStatus,
            false,
            prediction.Platform);
    }

    private static string RouteId(string lineCode) => $"MTR:{lineCode.ToUpperInvariant()}";

    private static string StationId(string lineCode, string stationCode) =>
        $"MTR:{lineCode.ToUpperInvariant()}:{stationCode.ToUpperInvariant()}";
}
