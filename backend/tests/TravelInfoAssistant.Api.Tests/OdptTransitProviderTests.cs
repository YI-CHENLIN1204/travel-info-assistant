using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Options;
using TravelInfoAssistant.Api.Providers.Odpt;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class OdptTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T00:00:00Z");

    [Fact]
    public async Task RoutesAndStations_IncludeBothSubwayOperatorsAndExcludeOtherToeiModes()
    {
        var client = new StubOdptApiClient
        {
            Railways =
            {
                ["odpt.Operator:TokyoMetro"] = [Railway("TokyoMetro", "Ginza", "銀座線")],
                ["odpt.Operator:Toei"] =
                [
                    Railway("Toei", "Asakusa", "浅草線"),
                    Railway("Toei", "TodenArakawa", "東京さくらトラム")
                ]
            },
            Stations =
            {
                ["odpt.Operator:TokyoMetro"] = [Station("TokyoMetro", "Ginza", "Ueno", "上野")],
                ["odpt.Operator:Toei"] =
                [
                    Station("Toei", "Asakusa", "Asakusa", "浅草"),
                    Station("Toei", "TodenArakawa", "Minowabashi", "三ノ輪橋")
                ]
            }
        };
        var provider = CreateProvider(client);

        var routes = await provider.GetMetroRoutesAsync(CancellationToken.None);
        var stations = await provider.GetMetroStationsAsync(CancellationToken.None);

        Assert.Equal(2, routes.Data.Count);
        Assert.Contains(routes.Data, item => item.Id == "odpt.Railway:TokyoMetro.Ginza");
        Assert.Contains(routes.Data, item =>
            item.Id == "odpt.Railway:Toei.Asakusa" &&
            item.Operators.SequenceEqual(["都營地下鐵"]));
        Assert.DoesNotContain(routes.Data, item => item.Id.Contains("TodenArakawa"));
        Assert.Equal(2, stations.Data.Count);
        Assert.Contains(stations.Data, item => item.Id == "odpt.Station:Toei.Asakusa.Asakusa");
        Assert.DoesNotContain(stations.Data, item => item.Id.Contains("TodenArakawa"));
    }

    [Fact]
    public async Task Departures_UseTheOperatorBelongingToTheSelectedToeiStation()
    {
        const string stationId = "odpt.Station:Toei.Asakusa.Asakusa";
        var client = new StubOdptApiClient
        {
            Stations =
            {
                ["odpt.Operator:Toei"] = [Station("Toei", "Asakusa", "Asakusa", "浅草")]
            },
            ReferencedStations =
            [
                Station("Keisei", "Main", "KeiseiTakasago", "京成高砂")
            ],
            Timetables =
            {
                [("odpt.Operator:Toei", stationId)] =
                [
                    new OdptStationTimetable
                    {
                        SameAs = "odpt.StationTimetable:Toei.Asakusa.Asakusa.Weekday",
                        Railway = "odpt.Railway:Toei.Asakusa",
                        RailwayTitle = new OdptLocalizedTitle { Ja = "浅草線" },
                        Station = stationId,
                        StationTitle = new OdptLocalizedTitle { Ja = "浅草" },
                        Calendar = "odpt.Calendar:Weekday",
                        Objects =
                        [
                            new OdptStationTimetableObject
                            {
                                DepartureTime = "10:00",
                                DestinationStations =
                                [
                                    "odpt.Station:Keisei.Main.KeiseiTakasago"
                                ]
                            }
                        ]
                    }
                ]
            }
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroDeparturesAsync(
            stationId,
            CancellationToken.None);

        var departure = Assert.Single(result.Data);
        Assert.Equal("浅草線", departure.LineName);
        Assert.Equal("京成高砂", departure.DestinationName);
        Assert.Equal(
            [("odpt.Operator:Toei", stationId)],
            client.TimetableRequests);
        Assert.Equal(
            ["odpt.Station:Keisei.Main.KeiseiTakasago"],
            Assert.Single(client.ReferencedStationRequests));
    }

    [Fact]
    public async Task ToeiDepartures_AddValidLiveDelayToScheduledTime()
    {
        const string stationId = "odpt.Station:Toei.Asakusa.Asakusa";
        const string trainId = "odpt.Train:Toei.Asakusa.101T";
        var client = new StubOdptApiClient
        {
            Stations =
            {
                ["odpt.Operator:Toei"] = [Station("Toei", "Asakusa", "Asakusa", "浅草")]
            },
            Timetables =
            {
                [("odpt.Operator:Toei", stationId)] =
                [
                    new OdptStationTimetable
                    {
                        Railway = "odpt.Railway:Toei.Asakusa",
                        Station = stationId,
                        StationTitle = new OdptLocalizedTitle { Ja = "浅草" },
                        Objects =
                        [
                            new OdptStationTimetableObject
                            {
                                DepartureTime = "10:00",
                                Train = trainId,
                                TrainNumber = "101T"
                            }
                        ]
                    }
                ]
            },
            Trains =
            {
                ["odpt.Operator:Toei"] =
                [
                    new OdptTrain
                    {
                        SameAs = trainId,
                        Operator = "odpt.Operator:Toei",
                        Railway = "odpt.Railway:Toei.Asakusa",
                        TrainNumber = "101T",
                        Delay = 120,
                        UpdatedAt = Now,
                        ValidUntil = Now.AddMinutes(1)
                    }
                ]
            }
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroDeparturesAsync(
            stationId,
            CancellationToken.None);

        var departure = Assert.Single(result.Data);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:00:00+09:00"), departure.ScheduledAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T10:02:00+09:00"), departure.EstimatedAt);
        Assert.Equal("延誤 2 分鐘", departure.ServiceStatus);
        Assert.Equal(Now, departure.SourceUpdatedAt);
        Assert.Equal("realtime", result.DataStatus);
        Assert.Equal(["odpt.Operator:Toei"], client.TrainRequests);
    }

    [Fact]
    public async Task TokyoMetroDepartures_RemainScheduledWithoutAConfiguredLiveFeed()
    {
        const string stationId = "odpt.Station:TokyoMetro.Ginza.Ueno";
        var client = new StubOdptApiClient
        {
            Stations =
            {
                ["odpt.Operator:TokyoMetro"] =
                    [Station("TokyoMetro", "Ginza", "Ueno", "上野")]
            },
            Timetables =
            {
                [("odpt.Operator:TokyoMetro", stationId)] =
                [
                    new OdptStationTimetable
                    {
                        Railway = "odpt.Railway:TokyoMetro.Ginza",
                        Station = stationId,
                        Objects =
                        [
                            new OdptStationTimetableObject
                            {
                                DepartureTime = "10:00",
                                TrainNumber = "A100"
                            }
                        ]
                    }
                ]
            }
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroDeparturesAsync(
            stationId,
            CancellationToken.None);

        Assert.Null(Assert.Single(result.Data).EstimatedAt);
        Assert.Equal("scheduled", result.DataStatus);
        Assert.Empty(client.TrainRequests);
    }

    [Theory]
    [InlineData("TokyoMetro", "Ginza", "Ueno", false)]
    [InlineData("Toei", "Asakusa", "Asakusa", true)]
    public async Task TokyoSubwayDepartures_ReportScheduledLastDepartureAfterServiceEnds(
        string operatorKey,
        string railwayKey,
        string stationKey,
        bool expectsLiveFeed)
    {
        var operatorId = $"odpt.Operator:{operatorKey}";
        var stationId = $"odpt.Station:{operatorKey}.{railwayKey}.{stationKey}";
        var now = DateTimeOffset.Parse("2026-10-01T14:00:00Z");
        var client = new StubOdptApiClient
        {
            Stations =
            {
                [operatorId] =
                    [Station(operatorKey, railwayKey, stationKey, stationKey)]
            },
            Timetables =
            {
                [(operatorId, stationId)] =
                [
                    new OdptStationTimetable
                    {
                        Railway = $"odpt.Railway:{operatorKey}.{railwayKey}",
                        Station = stationId,
                        Calendar = "odpt.Calendar:Weekday",
                        Objects =
                        [
                            new OdptStationTimetableObject
                            {
                                ArrivalTime = "22:40",
                                DepartureTime = "22:43"
                            }
                        ]
                    }
                ]
            }
        };
        var provider = CreateProvider(client, now);

        var result = await provider.GetMetroDeparturesAsync(
            stationId,
            CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T13:43:00Z"), result.LastDepartureAt);
        if (expectsLiveFeed)
        {
            Assert.Contains(operatorId, client.TrainRequests);
        }
        else
        {
            Assert.Empty(client.TrainRequests);
        }
    }

    [Fact]
    public async Task Status_CombinesBothOperatorsAndExcludesOtherToeiModes()
    {
        var client = new StubOdptApiClient
        {
            TrainInformation =
            {
                ["odpt.Operator:TokyoMetro"] = [Status("TokyoMetro", "Ginza", "銀座線")],
                ["odpt.Operator:Toei"] =
                [
                    Status("Toei", "Asakusa", "浅草線"),
                    Status("Toei", "TodenArakawa", "東京さくらトラム")
                ]
            }
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroStatusAsync(CancellationToken.None);

        Assert.Equal("realtime", result.DataStatus);
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, item => item.LineId == "odpt.Railway:Toei.Asakusa");
        Assert.DoesNotContain(result.Data, item => item.LineId?.Contains("TodenArakawa") == true);
    }

    [Fact]
    public async Task Routes_PreserveTokyoMetroWhenToeiIsTemporarilyUnavailable()
    {
        var client = new StubOdptApiClient
        {
            Railways =
            {
                ["odpt.Operator:TokyoMetro"] = [Railway("TokyoMetro", "Ginza", "銀座線")]
            },
            FailedRailwayOperators = { "odpt.Operator:Toei" }
        };
        var provider = CreateProvider(client);

        var result = await provider.GetMetroRoutesAsync(CancellationToken.None);

        Assert.Single(result.Data);
        Assert.Equal("scheduled", result.DataStatus);
        Assert.Contains("都營地下鐵", result.Message);
    }

    private static OdptTransitProvider CreateProvider(
        StubOdptApiClient client,
        DateTimeOffset? now = null) =>
        new(
            client,
            new PassThroughProviderCache(),
            Microsoft.Extensions.Options.Options.Create(
                new OdptOptions { ConsumerKey = "not-a-real-key" }),
            new FixedTimeProvider(now ?? Now),
            NullLogger<OdptTransitProvider>.Instance);

    private static OdptRailway Railway(string operatorKey, string railwayKey, string name) =>
        new()
        {
            SameAs = $"odpt.Railway:{operatorKey}.{railwayKey}",
            Operator = $"odpt.Operator:{operatorKey}",
            RailwayTitle = new OdptLocalizedTitle { Ja = name, En = $"{railwayKey} Line" },
            UpdatedAt = Now
        };

    private static OdptStation Station(
        string operatorKey,
        string railwayKey,
        string stationKey,
        string name) =>
        new()
        {
            SameAs = $"odpt.Station:{operatorKey}.{railwayKey}.{stationKey}",
            Operator = $"odpt.Operator:{operatorKey}",
            Railway = $"odpt.Railway:{operatorKey}.{railwayKey}",
            StationTitle = new OdptLocalizedTitle { Ja = name },
            UpdatedAt = Now
        };

    private static OdptTrainInformation Status(
        string operatorKey,
        string railwayKey,
        string name) =>
        new()
        {
            SameAs = $"odpt.TrainInformation:{operatorKey}.{railwayKey}",
            Operator = $"odpt.Operator:{operatorKey}",
            Railway = $"odpt.Railway:{operatorKey}.{railwayKey}",
            RailwayTitle = new OdptLocalizedTitle { Ja = name },
            TrainInformationText = new OdptLocalizedTitle { Ja = "平常どおり運転しています。" },
            UpdatedAt = Now,
            ValidUntil = Now.AddMinutes(5)
        };

    private sealed class StubOdptApiClient : IOdptApiClient
    {
        public Dictionary<string, IReadOnlyList<OdptRailway>> Railways { get; } = [];
        public Dictionary<string, IReadOnlyList<OdptStation>> Stations { get; } = [];
        public IReadOnlyList<OdptStation> ReferencedStations { get; init; } = [];
        public Dictionary<(string Operator, string Station), IReadOnlyList<OdptStationTimetable>>
            Timetables { get; } = [];
        public Dictionary<string, IReadOnlyList<OdptTrainInformation>> TrainInformation { get; } = [];
        public Dictionary<string, IReadOnlyList<OdptTrain>> Trains { get; } = [];
        public HashSet<string> FailedRailwayOperators { get; init; } = [];
        public List<(string Operator, string Station)> TimetableRequests { get; } = [];
        public List<IReadOnlyList<string>> ReferencedStationRequests { get; } = [];
        public List<string> TrainRequests { get; } = [];

        public Task<OdptHttpResult<IReadOnlyList<OdptCalendar>>> GetCalendarsAsync(
            CancellationToken cancellationToken) =>
            Result<OdptCalendar>([]);

        public Task<OdptHttpResult<IReadOnlyList<OdptRailway>>> GetRailwaysAsync(
            string operatorId,
            CancellationToken cancellationToken)
        {
            if (FailedRailwayOperators.Contains(operatorId))
            {
                throw new OdptProviderException("Unavailable for test.", 503);
            }

            return Result(Get(Railways, operatorId));
        }

        public Task<OdptHttpResult<IReadOnlyList<OdptStation>>> GetStationsAsync(
            string operatorId,
            CancellationToken cancellationToken) =>
            Result(Get(Stations, operatorId));

        public Task<OdptHttpResult<IReadOnlyList<OdptStation>>> GetStationsByIdsAsync(
            IReadOnlyCollection<string> stationIds,
            CancellationToken cancellationToken)
        {
            ReferencedStationRequests.Add(stationIds.ToList());
            return Result(
                ReferencedStations
                    .Where(station => station.SameAs is not null &&
                                      stationIds.Contains(
                                          station.SameAs,
                                          StringComparer.OrdinalIgnoreCase))
                    .ToList());
        }

        public Task<OdptHttpResult<IReadOnlyList<OdptStationTimetable>>> GetStationTimetablesAsync(
            string operatorId,
            string stationId,
            CancellationToken cancellationToken)
        {
            TimetableRequests.Add((operatorId, stationId));
            return Result(Get(Timetables, (operatorId, stationId)));
        }

        public Task<OdptHttpResult<IReadOnlyList<OdptTrainInformation>>> GetTrainInformationAsync(
            string operatorId,
            CancellationToken cancellationToken) =>
            Result(Get(TrainInformation, operatorId));

        public Task<OdptHttpResult<IReadOnlyList<OdptTrain>>> GetTrainsAsync(
            string operatorId,
            CancellationToken cancellationToken)
        {
            TrainRequests.Add(operatorId);
            return Result(Get(Trains, operatorId));
        }

        private static IReadOnlyList<T> Get<TKey, T>(
            IReadOnlyDictionary<TKey, IReadOnlyList<T>> values,
            TKey key) where TKey : notnull =>
            values.TryGetValue(key, out var result) ? result : [];

        private static Task<OdptHttpResult<IReadOnlyList<T>>> Result<T>(
            IReadOnlyList<T> data) =>
            Task.FromResult(new OdptHttpResult<IReadOnlyList<T>>(data, Now));
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
