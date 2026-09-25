using System.Text.Json;
using TravelInfoAssistant.Api.Providers.Odpt;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class OdptTransitMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MapRailway_MapsOfficialTitleAndTerminalStations()
    {
        const string json = """
            {
              "owl:sameAs": "odpt.Railway:TokyoMetro.Ginza",
              "dc:title": "銀座線",
              "odpt:railwayTitle": { "ja": "銀座線", "en": "Ginza Line" },
              "odpt:operator": "odpt.Operator:TokyoMetro",
              "odpt:stationOrder": [
                {
                  "odpt:index": 2,
                  "odpt:station": "odpt.Station:TokyoMetro.Ginza.Asakusa",
                  "odpt:stationTitle": { "ja": "浅草", "en": "Asakusa" }
                },
                {
                  "odpt:index": 1,
                  "odpt:station": "odpt.Station:TokyoMetro.Ginza.Shibuya",
                  "odpt:stationTitle": { "ja": "渋谷", "en": "Shibuya" }
                }
              ]
            }
            """;

        var railway = JsonSerializer.Deserialize<OdptRailway>(json, JsonOptions);
        var result = OdptTransitMapper.MapRailway(Assert.IsType<OdptRailway>(railway));

        Assert.NotNull(result);
        Assert.Equal("odpt.Railway:TokyoMetro.Ginza", result.Id);
        Assert.Equal("銀座線", result.NameZh);
        Assert.Equal("Ginza Line", result.NameEn);
        Assert.Equal("渋谷", result.OriginName);
        Assert.Equal("浅草", result.DestinationName);
        Assert.Equal(2, result.Directions.Count);
        Assert.Equal("Tokyo Metro", Assert.Single(result.Operators));
    }

    [Fact]
    public void MapStation_MapsCodeCoordinatesAndRailway()
    {
        const string json = """
            {
              "owl:sameAs": "odpt.Station:TokyoMetro.Ginza.Ueno",
              "dc:title": "上野",
              "odpt:stationTitle": { "ja": "上野", "en": "Ueno" },
              "odpt:stationCode": "G16",
              "odpt:operator": "odpt.Operator:TokyoMetro",
              "odpt:railway": "odpt.Railway:TokyoMetro.Ginza",
              "geo:long": 139.777122,
              "geo:lat": 35.711482
            }
            """;

        var station = JsonSerializer.Deserialize<OdptStation>(json, JsonOptions);
        var result = OdptTransitMapper.MapStation(Assert.IsType<OdptStation>(station));

        Assert.NotNull(result);
        Assert.Equal("上野", result.NameZh);
        Assert.Equal("Ueno", result.NameEn);
        Assert.Equal("G16", result.Code);
        Assert.Equal("銀座線", result.RailwayName);
        Assert.Equal(35.711482, result.Latitude);
        Assert.Equal(139.777122, result.Longitude);
    }
}
