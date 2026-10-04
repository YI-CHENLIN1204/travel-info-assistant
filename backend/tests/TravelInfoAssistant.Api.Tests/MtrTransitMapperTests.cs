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
    public void MapsBranchedLineDirectionsToPublicDestinations()
    {
        IReadOnlyList<MtrStationRow> rows =
        [
            new("TKL", "TKS-UT", "TIK", "49", "調景嶺", "Tiu Keng Leng", 1),
            new("TKL", "TKS-UT", "TKO", "50", "將軍澳", "Tseung Kwan O", 2),
            new("TKL", "TKS-UT", "LHP", "57", "康城", "LOHAS Park", 3),
            new("TKL", "UT", "NOP", "31", "北角", "North Point", 1),
            new("TKL", "UT", "TIK", "49", "調景嶺", "Tiu Keng Leng", 2),
            new("TKL", "UT", "TKO", "50", "將軍澳", "Tseung Kwan O", 3),
            new("TKL", "UT", "HAH", "51", "坑口", "Hang Hau", 4),
            new("TKL", "UT", "POA", "52", "寶琳", "Po Lam", 5),
            new("TKL", "TKS-DT", "LHP", "57", "康城", "LOHAS Park", 1),
            new("TKL", "TKS-DT", "TKO", "50", "將軍澳", "Tseung Kwan O", 2),
            new("TKL", "TKS-DT", "TIK", "49", "調景嶺", "Tiu Keng Leng", 3),
            new("TKL", "DT", "POA", "52", "寶琳", "Po Lam", 1),
            new("TKL", "DT", "HAH", "51", "坑口", "Hang Hau", 2),
            new("TKL", "DT", "TKO", "50", "將軍澳", "Tseung Kwan O", 3),
            new("TKL", "DT", "TIK", "49", "調景嶺", "Tiu Keng Leng", 4),
            new("TKL", "DT", "NOP", "31", "北角", "North Point", 5)
        ];

        var route = Assert.Single(MtrTransitMapper.MapRoutes(rows));

        Assert.Collection(
            route.Directions,
            northPoint =>
            {
                Assert.Equal(0, northPoint.Direction);
                Assert.Equal("北角", northPoint.DestinationName);
                Assert.Equal("寶琳／康城", northPoint.OriginName);
            },
            poLamOrLohasPark =>
            {
                Assert.Equal(1, poLamOrLohasPark.Direction);
                Assert.Equal("寶琳／康城", poLamOrLohasPark.DestinationName);
                Assert.Equal("北角", poLamOrLohasPark.OriginName);
            });
    }

    [Fact]
    public void MapsMultipleFullLengthBranchTerminalsWithoutLineSpecificOverrides()
    {
        IReadOnlyList<MtrStationRow> rows =
        [
            new("EAL", "LMC-UT", "ADM", "2", "金鐘", "Admiralty", 1),
            new("EAL", "LMC-UT", "SHS", "75", "上水", "Sheung Shui", 2),
            new("EAL", "LMC-UT", "LMC", "78", "落馬洲", "Lok Ma Chau", 3),
            new("EAL", "DT", "LOW", "76", "羅湖", "Lo Wu", 1),
            new("EAL", "DT", "SHS", "75", "上水", "Sheung Shui", 2),
            new("EAL", "DT", "ADM", "2", "金鐘", "Admiralty", 3),
            new("EAL", "UT", "ADM", "2", "金鐘", "Admiralty", 1),
            new("EAL", "UT", "SHS", "75", "上水", "Sheung Shui", 2),
            new("EAL", "UT", "LOW", "76", "羅湖", "Lo Wu", 3),
            new("EAL", "LMC-DT", "LMC", "78", "落馬洲", "Lok Ma Chau", 1),
            new("EAL", "LMC-DT", "SHS", "75", "上水", "Sheung Shui", 2),
            new("EAL", "LMC-DT", "ADM", "2", "金鐘", "Admiralty", 3)
        ];

        var route = Assert.Single(MtrTransitMapper.MapRoutes(rows));

        Assert.Equal("金鐘", route.Directions[0].DestinationName);
        Assert.Equal("羅湖／落馬洲", route.Directions[1].DestinationName);
    }

    [Fact]
    public void MapsUpAndDownBranchArrivalsToTheirPublicDirections()
    {
        IReadOnlyList<MtrStationRow> rows =
        [
            new("TKL", "DT", "NOP", "31", "北角", "North Point", 1),
            new("TKL", "DT", "TKO", "50", "將軍澳", "Tseung Kwan O", 2),
            new("TKL", "UT", "POA", "52", "寶琳", "Po Lam", 1),
            new("TKL", "TKS-UT", "LHP", "57", "康城", "LOHAS Park", 1)
        ];
        var response = new MtrScheduleResponse
        {
            SystemTime = "2026-10-02 12:00:00",
            IsDelay = "N",
            Status = 1,
            Data = new Dictionary<string, MtrStationSchedule>
            {
                ["TKL-TKO"] = new()
                {
                    Up =
                    [
                        new MtrTrainPrediction
                        {
                            Sequence = "1",
                            DestinationCode = "POA",
                            Time = "2026-10-02 12:03:00",
                            Valid = "Y"
                        },
                        new MtrTrainPrediction
                        {
                            Sequence = "2",
                            DestinationCode = "LHP",
                            Time = "2026-10-02 12:06:00",
                            Valid = "Y"
                        }
                    ],
                    Down =
                    [
                        new MtrTrainPrediction
                        {
                            Sequence = "1",
                            DestinationCode = "NOP",
                            Time = "2026-10-02 12:04:00",
                            Valid = "Y"
                        }
                    ]
                }
            }
        };

        var arrivals = MtrTransitMapper.MapArrivals("TKL", "TKO", response, rows);

        Assert.Collection(
            arrivals,
            poLam =>
            {
                Assert.Equal("寶琳", poLam.DestinationName);
                Assert.Equal(1, poLam.Direction);
            },
            northPoint =>
            {
                Assert.Equal("北角", northPoint.DestinationName);
                Assert.Equal(0, northPoint.Direction);
            },
            lohasPark =>
            {
                Assert.Equal("康城", lohasPark.DestinationName);
                Assert.Equal(1, lohasPark.Direction);
            });
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
                    Down =
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
        Assert.Equal(0, arrival.Direction);
        Assert.Equal("1", arrival.Platform);
        Assert.Equal("列車延誤", arrival.ServiceStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T12:03:00+08:00"), arrival.EstimatedAt);
    }

    [Fact]
    public void ReportsScheduledLastDepartureAfterServiceEndsPastMidnight()
    {
        IReadOnlyList<MtrLastTrainSchedule> schedules =
        [
            new("TWL", "1", "0054"),
            new("TWL", "25", "0104"),
            new("ISL", "37", "0108")
        ];

        var result = MtrTransitMapper.GetEndedServiceDayLastDeparture(
            "TWL",
            schedules,
            DateTimeOffset.Parse("2026-10-02T01:20:00+08:00"));

        Assert.Equal(DateTimeOffset.Parse("2026-10-02T01:04:00+08:00"), result);
    }

    [Theory]
    [InlineData("2026-10-02T00:50:00+08:00")]
    [InlineData("2026-10-02T12:00:00+08:00")]
    public void DoesNotReportEndedWhileScheduledLastDepartureIsStillAhead(string now)
    {
        var result = MtrTransitMapper.GetEndedServiceDayLastDeparture(
            "TWL",
            [new("TWL", "25", "0054")],
            DateTimeOffset.Parse(now));

        Assert.Null(result);
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
