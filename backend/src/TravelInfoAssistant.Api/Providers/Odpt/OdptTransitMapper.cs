using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.Odpt;

public static class OdptTransitMapper
{
    private static readonly IReadOnlyDictionary<string, string> RailwayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ginza"] = "銀座線",
            ["Marunouchi"] = "丸ノ内線",
            ["Hibiya"] = "日比谷線",
            ["Tozai"] = "東西線",
            ["Chiyoda"] = "千代田線",
            ["Yurakucho"] = "有楽町線",
            ["Hanzomon"] = "半蔵門線",
            ["Namboku"] = "南北線",
            ["Fukutoshin"] = "副都心線"
        };

    public static TransitRouteResponse? MapRailway(OdptRailway railway)
    {
        var id = railway.SameAs?.Trim();
        var nameZh = railway.RailwayTitle?.Ja?.Trim() ?? railway.Title?.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        var stations = railway.StationOrder
            .OrderBy(item => item.Index)
            .Where(item => !string.IsNullOrWhiteSpace(item.StationTitle?.Ja))
            .ToList();
        var origin = stations.FirstOrDefault()?.StationTitle?.Ja;
        var destination = stations.LastOrDefault()?.StationTitle?.Ja;
        var directions = new List<TransitDirectionResponse>();
        if (!string.IsNullOrWhiteSpace(origin) || !string.IsNullOrWhiteSpace(destination))
        {
            directions.Add(new TransitDirectionResponse(0, destination, origin, destination));
            directions.Add(new TransitDirectionResponse(1, origin, destination, origin));
        }

        return new TransitRouteResponse(
            id,
            nameZh,
            railway.RailwayTitle?.En?.Trim(),
            origin,
            destination,
            ["Tokyo Metro"],
            directions);
    }

    public static MetroStationResponse? MapStation(OdptStation station)
    {
        var id = station.SameAs?.Trim();
        var nameZh = station.StationTitle?.Ja?.Trim() ?? station.Title?.Trim();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(nameZh))
        {
            return null;
        }

        return new MetroStationResponse(
            id,
            nameZh,
            station.StationTitle?.En?.Trim(),
            null,
            station.Latitude,
            station.Longitude,
            station.StationCode?.Trim(),
            station.Railway?.Trim(),
            GetRailwayName(station.Railway));
    }

    private static string? GetRailwayName(string? railwayId)
    {
        var key = railwayId?.Split('.').LastOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return RailwayNames.TryGetValue(key, out var name) ? name : key;
    }
}
