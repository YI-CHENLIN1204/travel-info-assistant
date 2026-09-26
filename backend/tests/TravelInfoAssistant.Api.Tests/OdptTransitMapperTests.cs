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

    [Fact]
    public void MapStationDepartures_UsesSaturdayTimetableAndFutureTrips()
    {
        const string json = """
            [
              {
                "owl:sameAs": "odpt.StationTimetable:TokyoMetro.Ginza.Ueno.Outbound.Weekday",
                "dc:date": "2026-09-20T12:00:00+09:00",
                "odpt:railway": "odpt.Railway:TokyoMetro.Ginza",
                "odpt:station": "odpt.Station:TokyoMetro.Ginza.Ueno",
                "odpt:railDirection": "odpt.RailDirection:TokyoMetro.Asakusa",
                "odpt:calendar": "odpt.Calendar:Weekday",
                "odpt:stationTimetableObject": [
                  {
                    "odpt:departureTime": "10:00",
                    "odpt:destinationStation": ["odpt.Station:TokyoMetro.Ginza.Asakusa"]
                  }
                ]
              },
              {
                "owl:sameAs": "odpt.StationTimetable:TokyoMetro.Ginza.Ueno.Outbound.SaturdayHoliday",
                "dc:date": "2026-09-20T12:00:00+09:00",
                "odpt:railway": "odpt.Railway:TokyoMetro.Ginza",
                "odpt:railwayTitle": { "ja": "銀座線", "en": "Ginza Line" },
                "odpt:station": "odpt.Station:TokyoMetro.Ginza.Ueno",
                "odpt:stationTitle": { "ja": "上野", "en": "Ueno" },
                "odpt:railDirection": "odpt.RailDirection:TokyoMetro.Asakusa",
                "odpt:calendar": "odpt.Calendar:SaturdayHoliday",
                "odpt:stationTimetableObject": [
                  {
                    "odpt:departureTime": "08:30",
                    "odpt:destinationStation": ["odpt.Station:TokyoMetro.Ginza.Asakusa"]
                  },
                  {
                    "odpt:departureTime": "09:30",
                    "odpt:destinationStation": ["odpt.Station:TokyoMetro.Ginza.Asakusa"],
                    "odpt:trainNumber": "B901",
                    "odpt:platformNumber": "1",
                    "odpt:isLast": true
                  }
                ]
              }
            ]
            """;

        var timetables = JsonSerializer.Deserialize<IReadOnlyList<OdptStationTimetable>>(
            json,
            JsonOptions);
        var result = OdptTransitMapper.MapStationDepartures(
            Assert.IsAssignableFrom<IReadOnlyList<OdptStationTimetable>>(timetables),
            [],
            new Dictionary<string, string>
            {
                ["odpt.Station:TokyoMetro.Ginza.Asakusa"] = "浅草"
            },
            new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero));

        var departure = Assert.Single(result);
        Assert.Equal("上野", departure.StopName);
        Assert.Equal("銀座線", departure.LineName);
        Assert.Equal("浅草", departure.DestinationName);
        Assert.Equal("B901", departure.RouteName);
        Assert.Equal("1", departure.Platform);
        Assert.Equal(new DateTimeOffset(2026, 9, 26, 9, 30, 0, TimeSpan.FromHours(9)), departure.ScheduledAt);
        Assert.True(departure.IsLastService);
    }

    [Fact]
    public void MapStationDepartures_PrefersExplicitHolidayCalendar()
    {
        var stationId = "odpt.Station:TokyoMetro.Ginza.Ueno";
        var timetables = new List<OdptStationTimetable>
        {
            CreateTimetable(stationId, "odpt.Calendar:Weekday", "10:00"),
            CreateTimetable(stationId, "odpt.Calendar:Holiday", "11:00")
        };
        var calendars = new List<OdptCalendar>
        {
            new()
            {
                SameAs = "odpt.Calendar:Holiday",
                Days = [new DateOnly(2026, 10, 2)]
            }
        };

        var result = OdptTransitMapper.MapStationDepartures(
            timetables,
            calendars,
            new Dictionary<string, string>(),
            new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));

        var departure = Assert.Single(result);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.FromHours(9)), departure.ScheduledAt);
    }

    private static OdptStationTimetable CreateTimetable(
        string stationId,
        string calendar,
        string departureTime) =>
        new()
        {
            SameAs = $"odpt.StationTimetable:Test:{calendar}",
            Railway = "odpt.Railway:TokyoMetro.Ginza",
            Station = stationId,
            Calendar = calendar,
            Objects =
            [
                new OdptStationTimetableObject
                {
                    DepartureTime = departureTime,
                    DestinationStations = ["odpt.Station:TokyoMetro.Ginza.Asakusa"]
                }
            ]
        };
}
