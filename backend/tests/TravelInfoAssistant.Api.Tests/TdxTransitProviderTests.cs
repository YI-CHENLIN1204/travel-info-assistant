using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.Tdx;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class TdxTransitProviderTests
{
    [Fact]
    public async Task GetBusRoutesAsync_CorrectsDuplicatedOutboundEndpointsForReturnDirection()
    {
        IReadOnlyList<TdxBusRoute> routes =
        [
            new TdxBusRoute
            {
                RouteUID = "TPE214",
                RouteID = "214",
                RouteName = Name("214"),
                DepartureStopNameZh = "中和",
                DestinationStopNameZh = "內湖",
                SubRoutes =
                [
                    new TdxBusSubRoute
                    {
                        Direction = 0,
                        SubRouteName = Name("214"),
                        DepartureStopNameZh = "中和",
                        DestinationStopNameZh = "內湖"
                    },
                    new TdxBusSubRoute
                    {
                        Direction = 1,
                        SubRouteName = Name("214"),
                        DepartureStopNameZh = "中和",
                        DestinationStopNameZh = "內湖"
                    }
                ]
            }
        ];
        var provider = CreateProvider([], [], routes);

        var result = await provider.GetBusRoutesAsync(CancellationToken.None);

        var route = Assert.Single(result.Data);
        Assert.Collection(
            route.Directions,
            outbound =>
            {
                Assert.Equal(0, outbound.Direction);
                Assert.Equal("中和", outbound.OriginName);
                Assert.Equal("內湖", outbound.DestinationName);
            },
            inbound =>
            {
                Assert.Equal(1, inbound.Direction);
                Assert.Equal("內湖", inbound.OriginName);
                Assert.Equal("中和", inbound.DestinationName);
            });
    }

    [Fact]
    public async Task GetBusStopsAsync_DeduplicatesSharedSubRouteStops()
    {
        var provider = CreateProvider(CreateStopFeed(), []);

        var result = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);

        Assert.Collection(
            result.Data,
            stop =>
            {
                Assert.Equal("TPE1001", stop.Id);
                Assert.Equal("龍川中心", stop.NameZh);
                Assert.Equal(1, stop.Sequence);
            },
            stop =>
            {
                Assert.Equal("TPE1002", stop.Id);
                Assert.Equal("中和高中", stop.NameZh);
                Assert.Equal(2, stop.Sequence);
            });
    }

    [Fact]
    public async Task GetBusArrivalsAsync_MatchesEquivalentStopIdFromAnotherSubRoute()
    {
        IReadOnlyList<TdxBusArrival> arrivals =
        [
            Arrival("TPE1001", "1001"),
            // TDX can repeat the same vehicle under the equivalent stop ID of another sub-route.
            // The provider must still return one user-facing arrival.
            Arrival("NWT9001", "9001")
        ];
        var provider = CreateProvider(CreateStopFeed(), arrivals);

        var result = await provider.GetBusArrivalsAsync(
            "214",
            0,
            "TPE1001",
            CancellationToken.None);

        var arrival = Assert.Single(result.Data);
        Assert.Equal("TPE1001", arrival.StopId);
        Assert.Equal("龍川中心", arrival.StopName);
        Assert.Equal("214", arrival.RouteName);
    }

    private static TdxBusArrival Arrival(string stopUid, string stopId) =>
        new()
        {
            PlateNumb = "ABC-123",
            StopUID = stopUid,
            StopID = stopId,
            StopName = Name("龍川中心"),
            RouteUID = "TPE214",
            RouteID = "214",
            RouteName = Name("214"),
            Direction = 0,
            EstimateTime = 300,
            StopStatus = 0,
            SrcUpdateTime = DateTimeOffset.Parse("2026-09-22T21:00:00+08:00")
        };

    [Fact]
    public async Task GetBusStopsAsync_KeepsDifferentBranchStopsAtSameSequence()
    {
        IReadOnlyList<TdxBusStopOfRoute> stops =
        [
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops = [Stop("TPE3001", "3001", "支線甲", 3)]
            },
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops = [Stop("TPE3002", "3002", "支線乙", 3)]
            }
        ];
        var provider = CreateProvider(stops, []);

        var result = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);

        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, stop => stop.NameZh == "支線甲");
        Assert.Contains(result.Data, stop => stop.NameZh == "支線乙");
    }

    [Fact]
    public async Task GetBusStopsAsync_ReturnsOnlyTheRequestedDirection()
    {
        IReadOnlyList<TdxBusStopOfRoute> stops =
        [
            new TdxBusStopOfRoute
            {
                Direction = 0,
                Stops =
                [
                    Stop("TPE4001", "4001", "中和", 1),
                    Stop("TPE4002", "4002", "內湖", 2)
                ]
            },
            new TdxBusStopOfRoute
            {
                Direction = 1,
                Stops =
                [
                    Stop("TPE5001", "5001", "內湖", 1),
                    Stop("TPE5002", "5002", "中和", 2)
                ]
            }
        ];
        var provider = CreateProvider(stops, []);

        var outbound = await provider.GetBusStopsAsync("214", 0, CancellationToken.None);
        var inbound = await provider.GetBusStopsAsync("214", 1, CancellationToken.None);

        Assert.Equal(["中和", "內湖"], outbound.Data.Select(item => item.NameZh));
        Assert.Equal(["內湖", "中和"], inbound.Data.Select(item => item.NameZh));
    }

    [Fact]
    public async Task GetMetroRoutesAsync_MapsBranchedLineTerminalsAndDirections()
    {
        var provider = CreateProviderWithResponses(new Dictionary<string, object>
        {
            ["v2/Rail/Metro/Line/TRTC"] = new[]
            {
                new TdxMetroLine
                {
                    LineID = "O",
                    LineName = Name("中和新蘆線")
                }
            },
            ["v2/Rail/Metro/StationOfRoute/TRTC"] = new[]
            {
                MetroRoute("O", "O-1", 0, ("O01", "南勢角"), ("O12", "大橋頭"), ("O21", "迴龍")),
                MetroRoute("O", "O-2", 0, ("O01", "南勢角"), ("O12", "大橋頭"), ("O54", "蘆洲")),
                MetroRoute("O", "O-3", 1, ("O21", "迴龍"), ("O12", "大橋頭"), ("O01", "南勢角")),
                MetroRoute("O", "O-4", 1, ("O54", "蘆洲"), ("O12", "大橋頭"), ("O01", "南勢角"))
            }
        });

        var result = await provider.GetMetroRoutesAsync(CancellationToken.None);

        var route = Assert.Single(result.Data);
        Assert.Equal("TDX:TRTC:O", route.Id);
        Assert.Equal("中和新蘆線", route.NameZh);
        Assert.Collection(
            route.Directions,
            outbound =>
            {
                Assert.Equal(0, outbound.Direction);
                Assert.Equal("南勢角", outbound.OriginName);
                Assert.Equal("迴龍／蘆洲", outbound.DestinationName);
            },
            inbound =>
            {
                Assert.Equal(1, inbound.Direction);
                Assert.Equal("迴龍／蘆洲", inbound.OriginName);
                Assert.Equal("南勢角", inbound.DestinationName);
            });
        Assert.Contains("迴龍", route.StationNames);
        Assert.Contains("蘆洲", route.StationNames);
    }

    [Fact]
    public async Task GetMetroStationsAsync_AssignsLineMembership()
    {
        var provider = CreateProviderWithResponses(new Dictionary<string, object>
        {
            ["v2/Rail/Metro/Line/TRTC"] = new[]
            {
                new TdxMetroLine
                {
                    LineID = "BL",
                    LineName = Name("板南線")
                }
            },
            ["v2/Rail/Metro/StationOfRoute/TRTC"] = new[]
            {
                MetroRoute("BL", "BL-1", 0, ("BL01", "頂埔"), ("BL12", "台北車站"))
            }
        });

        var result = await provider.GetMetroStationsAsync(CancellationToken.None);

        Assert.Collection(
            result.Data,
            station =>
            {
                Assert.Equal("BL01", station.Id);
                Assert.Equal("TDX:TRTC:BL", station.RailwayId);
                Assert.Equal("板南線", station.RailwayName);
            },
            station => Assert.Equal("BL12", station.Code));
    }

    [Fact]
    public async Task GetMetroStatusAsync_MapsNetworkWideOfficialAlert()
    {
        var updatedAt = DateTimeOffset.Parse("2026-10-02T10:00:00+08:00");
        var provider = CreateProviderWithResponses(new Dictionary<string, object>
        {
            ["v2/Rail/Metro/Alert/TRTC"] = new TdxMetroAlertResponse
            {
                SrcUpdateTime = updatedAt,
                SrcUpdateInterval = 60,
                Alerts =
                [
                    new TdxMetroAlert
                    {
                        AlertID = "0",
                        Title = "正常營運",
                        Description = "正常營運",
                        Status = 1,
                        Scope = new TdxMetroAlertScope(),
                        UpdateTime = updatedAt
                    }
                ]
            }
        });

        var result = await provider.GetMetroStatusAsync(
            "TDX:TRTC:BL",
            CancellationToken.None);

        var status = Assert.Single(result.Data);
        Assert.Equal("TDX:TRTC:BL", status.LineId);
        Assert.Equal("正常營運", status.MessageZh);
        Assert.Equal(updatedAt.AddSeconds(60), status.ValidUntil);
        Assert.Equal("realtime", result.DataStatus);
    }

    [Fact]
    public async Task GetMetroArrivalsAsync_ReportsScheduledLastDepartureAfterServiceEnds()
    {
        var now = DateTimeOffset.Parse("2026-09-23T15:00:00Z");
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v2/Rail/Metro/StationTimeTable/TRTC"] = new[]
                {
                    new TdxMetroStationTimetable
                    {
                        StationID = "BL12",
                        StationName = Name("臺北車站"),
                        LineID = "BL",
                        Direction = 0,
                        Timetables =
                        [
                            new TdxMetroTimetableEntry
                            {
                                Sequence = 1,
                                ArrivalTime = "22:10",
                                DepartureTime = "22:12"
                            },
                            new TdxMetroTimetableEntry
                            {
                                Sequence = 2,
                                ArrivalTime = "22:40",
                                DepartureTime = "22:43"
                            }
                        ]
                    }
                },
                ["v2/Rail/Metro/LiveBoard/TRTC"] = Array.Empty<TdxMetroLiveBoard>()
            },
            new FixedTimeProvider(now));

        var result = await provider.GetMetroArrivalsAsync("BL12", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T14:43:00Z"), result.LastDepartureAt);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task GetMetroArrivalsAsync_DoesNotReportServiceEndedWithoutATimetable()
    {
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v2/Rail/Metro/StationTimeTable/TRTC"] =
                    Array.Empty<TdxMetroStationTimetable>(),
                ["v2/Rail/Metro/LiveBoard/TRTC"] = Array.Empty<TdxMetroLiveBoard>()
            },
            new FixedTimeProvider(DateTimeOffset.Parse("2026-09-23T15:00:00Z")));

        var result = await provider.GetMetroArrivalsAsync("BL12", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Null(result.ServiceDayStatus);
        Assert.Null(result.LastDepartureAt);
        Assert.Equal("unavailable", result.DataStatus);
    }

    [Fact]
    public async Task GetRailStationsAsync_MapsAndOrdersStationsByOfficialLineSequence()
    {
        var provider = CreateProviderWithResponses(new Dictionary<string, object>
        {
            ["v3/Rail/TRA/Station"] = new TdxTraStationResponse
            {
                SrcUpdateTime = DateTimeOffset.Parse("2026-09-23T09:00:00+08:00"),
                Stations =
                [
                    new TdxTraStation
                    {
                        StationID = "3480",
                        StationName = new TdxLocalizedName { ZhTw = "斗南", En = "Dounan" },
                        StationAddress = "雲林縣斗南鎮"
                    },
                    new TdxTraStation
                    {
                        StationUID = "TRA3470",
                        StationID = "3470",
                        StationName = new TdxLocalizedName { ZhTw = "斗六", En = "Douliu" },
                        StationAddress = "雲林縣斗六市",
                        StationPosition = new TdxPosition
                        {
                            PositionLat = 23.7117,
                            PositionLon = 120.5412
                        }
                    }
                ]
            },
            ["v3/Rail/TRA/StationOfLine"] = new TdxTraStationOfLineResponse
            {
                SrcUpdateTime = DateTimeOffset.Parse("2026-09-23T08:00:00+08:00"),
                StationOfLines =
                [
                    new TdxTraStationOfLine
                    {
                        LineID = "WL",
                        Stations =
                        [
                            new TdxTraLineStation
                            {
                                StationID = "3470",
                                Sequence = 47,
                                CumulativeDistance = 260.6
                            },
                            new TdxTraLineStation
                            {
                                StationID = "3480",
                                Sequence = 48,
                                CumulativeDistance = 268.2
                            }
                        ]
                    }
                ]
            }
        });

        var result = await provider.GetRailStationsAsync(CancellationToken.None);

        Assert.Equal(["3470", "3480"], result.Data.Select(station => station.Id));
        var station = result.Data[0];
        Assert.Equal("斗六", station.NameZh);
        Assert.Equal("Douliu", station.NameEn);
        Assert.Equal(23.7117, station.Latitude);
        var position = Assert.Single(station.LinePositions);
        Assert.Equal("WL", position.LineId);
        Assert.Equal(47, position.Sequence);
        Assert.Equal(260.6, position.CumulativeDistance);
    }

    [Fact]
    public async Task GetRailArrivalsAsync_CombinesScheduleWithLiveDelayAndPlatform()
    {
        var now = DateTimeOffset.Parse("2026-09-23T02:00:00Z");
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v3/Rail/TRA/Station"] = new TdxTraStationResponse
                {
                    Stations =
                    [
                        new TdxTraStation
                        {
                            StationID = "1000",
                            StationName = Name("臺北"),
                            StationPosition = new TdxPosition { PositionLat = 25.0478 }
                        },
                        new TdxTraStation
                        {
                            StationID = "4400",
                            StationName = Name("高雄"),
                            StationPosition = new TdxPosition { PositionLat = 22.6398 }
                        }
                    ]
                },
                ["v3/Rail/TRA/StationOfLine"] = new TdxTraStationOfLineResponse
                {
                    StationOfLines =
                    [
                        new TdxTraStationOfLine
                        {
                            LineID = "WL",
                            Stations =
                            [
                                new TdxTraLineStation { StationID = "1000", Sequence = 1 },
                                new TdxTraLineStation { StationID = "4400", Sequence = 2 }
                            ]
                        }
                    ]
                },
                ["v3/Rail/TRA/DailyStationTimetable/Today/Station/1000"] =
                    new TdxTraDailyStationTimetableResponse
                    {
                        SrcUpdateTime = DateTimeOffset.Parse("2026-09-23T08:00:00+08:00"),
                        StationTimetables =
                        [
                            new TdxTraStationTimetable
                            {
                                StationID = "1000",
                                StationName = Name("臺北"),
                                Direction = 1,
                                TimeTables =
                                [
                                    new TdxTraTimetableEntry
                                    {
                                        Sequence = 1,
                                        TrainNo = "123",
                                        TrainTypeID = "1100",
                                        TrainTypeName = Name("自強"),
                                        DestinationStationID = "4400",
                                        DestinationStationName = Name("高雄"),
                                        ArrivalTime = "10:10"
                                    }
                                ]
                            }
                        ]
                    },
                ["v3/Rail/TRA/StationLiveBoard/Station/1000"] =
                    new TdxTraStationLiveBoardResponse
                    {
                        SrcUpdateTime = DateTimeOffset.Parse("2026-09-23T10:01:00+08:00"),
                        StationLiveBoards =
                        [
                            new TdxTraStationLiveBoard
                            {
                                StationID = "1000",
                                StationName = Name("臺北"),
                                TrainNo = "123",
                                Direction = 1,
                                TrainTypeID = "1100",
                                TrainTypeName = Name("自強"),
                                EndingStationID = "4400",
                                EndingStationName = Name("高雄"),
                                Platform = "2B",
                                ScheduleArrivalTime = "10:10",
                                DelayTime = 5,
                                RunningStatus = 1,
                                UpdateTime = DateTimeOffset.Parse("2026-09-23T10:01:00+08:00")
                            }
                        ]
                    }
            },
            new FixedTimeProvider(now));

        var result = await provider.GetRailArrivalsAsync("1000", CancellationToken.None);

        var arrival = Assert.Single(result.Data);
        Assert.Equal("rail", arrival.Mode);
        Assert.Equal("123", arrival.RouteName);
        Assert.Equal("自強", arrival.LineName);
        Assert.Equal("高雄", arrival.DestinationName);
        Assert.Equal(1, arrival.Direction);
        Assert.Equal("south", arrival.Heading);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T02:10:00Z"), arrival.ScheduledAt);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T02:15:00Z"), arrival.EstimatedAt);
        Assert.Equal("誤點 5 分", arrival.ServiceStatus);
        Assert.Equal("2B", arrival.Platform);
        Assert.Equal("realtime", result.DataStatus);
    }

    [Fact]
    public async Task GetRailArrivalsAsync_MergesLiveDataIntoNextTenTripsPerDirection()
    {
        var now = DateTimeOffset.Parse("2026-09-23T02:00:00Z");
        var northbound = Enumerable.Range(0, 12)
            .Select(index => new TdxTraTimetableEntry
            {
                Sequence = index + 1,
                TrainNo = $"N{index:00}",
                DestinationStationID = "1000",
                DestinationStationName = Name("基隆"),
                ArrivalTime = $"10:{10 + index:00}"
            })
            .ToList();
        var southbound = Enumerable.Range(0, 12)
            .Select(index => new TdxTraTimetableEntry
            {
                Sequence = index + 1,
                TrainNo = $"S{index:00}",
                DestinationStationID = "5000",
                DestinationStationName = Name("潮州"),
                ArrivalTime = $"18:{index:00}"
            })
            .ToList();
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v3/Rail/TRA/Station"] = new TdxTraStationResponse
                {
                    Stations =
                    [
                        new TdxTraStation
                        {
                            StationID = "3470",
                            StationName = Name("斗六"),
                            StationPosition = new TdxPosition { PositionLat = 23.7117 }
                        },
                        new TdxTraStation
                        {
                            StationID = "1000",
                            StationName = Name("基隆"),
                            StationPosition = new TdxPosition { PositionLat = 25.1310 }
                        },
                        new TdxTraStation
                        {
                            StationID = "5000",
                            StationName = Name("潮州"),
                            StationPosition = new TdxPosition { PositionLat = 22.5500 }
                        }
                    ]
                },
                ["v3/Rail/TRA/StationOfLine"] = new TdxTraStationOfLineResponse(),
                ["v3/Rail/TRA/DailyStationTimetable/Today/Station/3470"] =
                    new TdxTraDailyStationTimetableResponse
                    {
                        StationTimetables =
                        [
                            new TdxTraStationTimetable
                            {
                                StationID = "3470",
                                StationName = Name("斗六"),
                                Direction = 0,
                                TimeTables = northbound
                            },
                            new TdxTraStationTimetable
                            {
                                StationID = "3470",
                                StationName = Name("斗六"),
                                Direction = 1,
                                TimeTables = southbound
                            }
                        ]
                    },
                ["v3/Rail/TRA/StationLiveBoard/Station/3470"] =
                    new TdxTraStationLiveBoardResponse
                    {
                        StationLiveBoards =
                        [
                            new TdxTraStationLiveBoard
                            {
                                StationID = "3470",
                                StationName = Name("斗六"),
                                TrainNo = "N00",
                                Direction = 0,
                                EndingStationID = "1000",
                                EndingStationName = Name("基隆"),
                                ScheduleArrivalTime = "10:10",
                                DelayTime = 3,
                                RunningStatus = 1
                            }
                        ]
                    }
            },
            new FixedTimeProvider(now));

        var result = await provider.GetRailArrivalsAsync("3470", CancellationToken.None);

        Assert.Equal(20, result.Data.Count);
        Assert.Equal(10, result.Data.Count(item => item.Direction == 0));
        Assert.Equal(10, result.Data.Count(item => item.Direction == 1));
        Assert.Equal(10, result.Data.Count(item => item.Heading == "north"));
        Assert.Equal(10, result.Data.Count(item => item.Heading == "south"));
        var liveArrival = Assert.Single(result.Data, item => item.RouteName == "N00");
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T02:13:00Z"), liveArrival.EstimatedAt);
        Assert.Contains(result.Data, item =>
            item.RouteName == "S00" &&
            item.ScheduledAt == DateTimeOffset.Parse("2026-09-23T10:00:00Z"));
        Assert.DoesNotContain(result.Data, item => item.RouteName is "N10" or "N11" or "S10" or "S11");
        Assert.Equal("realtime", result.DataStatus);
    }

    [Fact]
    public async Task GetRailArrivalsAsync_ReportsScheduledLastDepartureAfterServiceEnds()
    {
        var now = DateTimeOffset.Parse("2026-09-23T15:00:00Z");
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v3/Rail/TRA/Station"] = new TdxTraStationResponse(),
                ["v3/Rail/TRA/StationOfLine"] = new TdxTraStationOfLineResponse(),
                ["v3/Rail/TRA/DailyStationTimetable/Today/Station/3470"] =
                    new TdxTraDailyStationTimetableResponse
                    {
                        StationTimetables =
                        [
                            new TdxTraStationTimetable
                            {
                                StationID = "3470",
                                StationName = Name("斗六"),
                                Direction = 0,
                                TimeTables =
                                [
                                    new TdxTraTimetableEntry
                                    {
                                        Sequence = 1,
                                        TrainNo = "150",
                                        ArrivalTime = "22:10",
                                        DepartureTime = "22:12"
                                    },
                                    new TdxTraTimetableEntry
                                    {
                                        Sequence = 2,
                                        TrainNo = "152",
                                        ArrivalTime = "22:40",
                                        DepartureTime = "22:43"
                                    }
                                ]
                            }
                        ]
                    },
                ["v3/Rail/TRA/StationLiveBoard/Station/3470"] =
                    new TdxTraStationLiveBoardResponse()
            },
            new FixedTimeProvider(now));

        var result = await provider.GetRailArrivalsAsync("3470", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T14:43:00Z"), result.LastDepartureAt);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task GetRailArrivalsAsync_DoesNotReportServiceEndedWithoutATimetable()
    {
        var provider = CreateProviderWithResponses(
            new Dictionary<string, object>
            {
                ["v3/Rail/TRA/Station"] = new TdxTraStationResponse(),
                ["v3/Rail/TRA/StationOfLine"] = new TdxTraStationOfLineResponse(),
                ["v3/Rail/TRA/DailyStationTimetable/Today/Station/3470"] =
                    new TdxTraDailyStationTimetableResponse(),
                ["v3/Rail/TRA/StationLiveBoard/Station/3470"] =
                    new TdxTraStationLiveBoardResponse()
            },
            new FixedTimeProvider(DateTimeOffset.Parse("2026-09-23T15:00:00Z")));

        var result = await provider.GetRailArrivalsAsync("3470", CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Null(result.ServiceDayStatus);
        Assert.Null(result.LastDepartureAt);
        Assert.Equal("unavailable", result.DataStatus);
    }

    private static TdxTransitProvider CreateProvider(
        IReadOnlyList<TdxBusStopOfRoute> stops,
        IReadOnlyList<TdxBusArrival> arrivals,
        IReadOnlyList<TdxBusRoute>? routes = null)
    {
        var responses = new Dictionary<string, object>
        {
            ["v2/Bus/Route/City/Taipei"] = routes ?? Array.Empty<TdxBusRoute>(),
            ["v2/Bus/StopOfRoute/City/Taipei/214"] = stops,
            ["v2/Bus/EstimatedTimeOfArrival/City/Taipei/214"] = arrivals
        };
        return CreateProviderWithResponses(responses, TimeProvider.System);
    }

    private static TdxTransitProvider CreateProviderWithResponses(
        IReadOnlyDictionary<string, object> responses,
        TimeProvider? timeProvider = null) =>
        new(
            new StubTdxApiClient(responses),
            new PassThroughProviderCache(),
            timeProvider ?? TimeProvider.System,
            NullLogger<TdxTransitProvider>.Instance);

    private static IReadOnlyList<TdxBusStopOfRoute> CreateStopFeed() =>
    [
        new TdxBusStopOfRoute
        {
            Direction = 0,
            Stops =
            [
                Stop("TPE1001", "1001", "龍川中心", 1),
                Stop("TPE1002", "1002", "中和高中", 2)
            ]
        },
        new TdxBusStopOfRoute
        {
            Direction = 0,
            Stops =
            [
                Stop("NWT9001", "9001", "龍川中心", 1),
                Stop("NWT9002", "9002", "中和高中", 2)
            ]
        }
    ];

    private static TdxBusStop Stop(
        string stopUid,
        string stopId,
        string name,
        int sequence) =>
        new()
        {
            StopUID = stopUid,
            StopID = stopId,
            StopName = Name(name),
            StopSequence = sequence
        };

    private static TdxLocalizedName Name(string value) => new() { ZhTw = value };

    private static TdxMetroStationOfRoute MetroRoute(
        string lineId,
        string routeId,
        int direction,
        params (string Id, string Name)[] stations) =>
        new()
        {
            LineID = lineId,
            RouteID = routeId,
            Direction = direction,
            Stations = stations
                .Select((station, index) => new TdxMetroRouteStation
                {
                    Sequence = index + 1,
                    StationID = station.Id,
                    StationName = Name(station.Name)
                })
                .ToList()
        };

    private sealed class StubTdxApiClient(IReadOnlyDictionary<string, object> responses)
        : ITdxApiClient
    {
        public Task<TdxHttpResult<T>> GetAsync<T>(
            string relativePath,
            IReadOnlyDictionary<string, string?> query,
            CancellationToken cancellationToken)
        {
            if (!responses.TryGetValue(relativePath, out var value) || value is not T typed)
            {
                throw new InvalidOperationException($"No stub response for {relativePath}.");
            }

            return Task.FromResult(new TdxHttpResult<T>(
                typed,
                null,
                DateTimeOffset.Parse("2026-09-22T13:00:00Z"),
                0));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class PassThroughProviderCache : IProviderCache
    {
        public async Task<ProviderQueryResult<T>> GetOrCreateAsync<T>(
            string key,
            TimeSpan freshFor,
            TimeSpan retainFor,
            Func<CancellationToken, Task<ProviderPayload<T>>> factory,
            CancellationToken cancellationToken)
        {
            var payload = await factory(cancellationToken);
            return new ProviderQueryResult<T>(
                payload.Data,
                payload.DataStatus,
                payload.SourceUpdatedAt,
                payload.FetchedAt,
                false,
                null);
        }
    }
}
