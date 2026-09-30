using TravelInfoAssistant.Api.Providers.Mtr;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MtrTransitMapperTests
{
    private static readonly IReadOnlyList<MtrStationRow> Rows =
    [
        new("TWL", "UT", "CEN", "1", "中環", "Central", 1),
        new("TWL", "UT", "ADM", "2", "金鐘", "Admiralty", 2),
        new("TWL", "UT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 3),
        new("TWL", "DT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 1),
        new("TWL", "DT", "ADM", "2", "金鐘", "Admiralty", 2),
        new("TWL", "DT", "CEN", "1", "中環", "Central", 3),
        new("ISL", "UT", "CEN", "1", "中環", "Central", 1)
    ];

    [Fact]
    public void MapsRoutesAndKeepsInterchangeStationsPerLine()
    {
        var routes = MtrTransitMapper.MapRoutes(Rows);
        var stations = MtrTransitMapper.MapStations(Rows);

        Assert.Equal(2, routes.Count);
        var tsuenWan = Assert.Single(routes, route => route.Id == "MTR:TWL");
        Assert.Equal("荃灣綫", tsuenWan.NameZh);
        Assert.Equal(["中環", "金鐘", "尖沙咀"], tsuenWan.StationNames);
        Assert.Contains(stations, station => station.Id == "MTR:TWL:CEN");
        Assert.Contains(stations, station => station.Id == "MTR:ISL:CEN");
    }

    [Fact]
    public void MapsRealtimeArrivalsWithDestinationPlatformAndDelay()
    {
        var response = new MtrScheduleResponse
        {
            SystemTime = "2026-10-01 12:00:00",
            IsDelay = "Y",
            Status = 1,
            Data = new Dictionary<string, MtrStationSchedule>
            {
                ["TWL-TST"] = new()
                {
                    SystemTime = "2026-10-01 12:00:00",
                    Up =
                    [
                        new MtrTrainPrediction
                        {
                            Sequence = "1",
                            DestinationCode = "CEN",
                            Platform = "1",
                            Time = "2026-10-01 12:03:00",
                            Valid = "Y"
                        }
                    ]
                }
            }
        };

        var arrival = Assert.Single(MtrTransitMapper.MapArrivals("TWL", "TST", response, Rows));

        Assert.Equal("中環", arrival.DestinationName);
        Assert.Equal("1", arrival.Platform);
        Assert.Equal("列車延誤", arrival.ServiceStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:03:00+08:00"), arrival.EstimatedAt);
    }

    [Theory]
    [InlineData("N", 1, "列車服務正常。")]
    [InlineData("Y", 1, "列車服務受延誤，請預留額外乘車時間。")]
    [InlineData("N", 0, "列車服務暫停")]
    public void MapsOfficialStatusMessage(string isDelay, int status, string expected)
    {
        var result = MtrTransitMapper.MapStatus(
            "TWL",
            new MtrScheduleResponse
            {
                SystemTime = "2026-10-01 12:00:00",
                IsDelay = isDelay,
                Status = status,
                Message = status == 0 ? "列車服務暫停" : "successful"
            });

        Assert.NotNull(result);
        Assert.Equal(expected, result.MessageZh);
        Assert.Equal("MTR:TWL", result.LineId);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:00:45+08:00"), result.ValidUntil);
    }
}
