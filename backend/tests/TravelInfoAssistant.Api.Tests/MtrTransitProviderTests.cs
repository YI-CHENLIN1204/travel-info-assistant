using Microsoft.Extensions.Logging.Abstractions;
using TravelInfoAssistant.Api.Providers.Mtr;
using TravelInfoAssistant.Api.Services.Transit;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MtrTransitProviderTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-01T12:00:00+08:00");

    [Fact]
    public async Task ProvidesCompleteRouteStationArrivalAndStatusFlow()
    {
        var client = new StubMtrApiClient
        {
            Network =
            [
                new("TWL", "UT", "CEN", "1", "中環", "Central", 1),
                new("TWL", "UT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 2),
                new("TWL", "DT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 1),
                new("TWL", "DT", "CEN", "1", "中環", "Central", 2)
            ],
            Schedule = new MtrScheduleResponse
            {
                SystemTime = "2026-10-01 12:00:00",
                IsDelay = "N",
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
                    },
                    ["TWL-CEN"] = new()
                    {
                        SystemTime = "2026-10-01 12:00:00"
                    }
                }
            }
        };
        var provider = CreateProvider(client);

        var routes = await provider.GetMetroRoutesAsync(CancellationToken.None);
        var stations = await provider.GetMetroStationsAsync(CancellationToken.None);
        var arrivals = await provider.GetMetroArrivalsAsync(
            "MTR:TWL:TST",
            CancellationToken.None);
        var status = await provider.GetMetroStatusAsync(
            "MTR:TWL",
            CancellationToken.None);

        Assert.Equal("香港港鐵開放數據", routes.Source);
        Assert.Equal("scheduled", routes.DataStatus);
        Assert.Equal("MTR:TWL", Assert.Single(routes.Data).Id);
        Assert.Contains(stations.Data, station => station.Id == "MTR:TWL:TST");
        Assert.Equal("realtime", arrivals.DataStatus);
        Assert.Equal("中環", Assert.Single(arrivals.Data).DestinationName);
        Assert.Equal("列車服務正常。", Assert.Single(status.Data).MessageZh);
        Assert.Contains(("TWL", "TST"), client.ScheduleRequests);
        Assert.Contains(("TWL", "CEN"), client.ScheduleRequests);
        Assert.Empty(client.LastTrainScheduleRequests);
    }

    [Fact]
    public async Task ReportsScheduledLastDepartureWhenDailyServiceHasEnded()
    {
        var now = DateTimeOffset.Parse("2026-10-02T01:20:00+08:00");
        var client = new StubMtrApiClient
        {
            Network =
            [
                new("TWL", "UT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 1)
            ],
            Schedule = new MtrScheduleResponse
            {
                SystemTime = "2026-10-02 01:20:00",
                Status = 1,
                Data = new Dictionary<string, MtrStationSchedule>
                {
                    ["TWL-TST"] = new()
                }
            },
            LastTrainSchedules =
            [
                new("TWL", "1", "0054"),
                new("TWL", "25", "0104")
            ]
        };
        var provider = CreateProvider(client, now);

        var result = await provider.GetMetroArrivalsAsync(
            "MTR:TWL:TST",
            CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Equal("ended", result.ServiceDayStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-02T01:04:00+08:00"), result.LastDepartureAt);
        Assert.Null(result.Message);
        Assert.Equal(["3"], client.LastTrainScheduleRequests);
    }

    [Fact]
    public async Task DoesNotReportServiceEndedWithoutAnOfficialLastTrainSchedule()
    {
        var client = new StubMtrApiClient
        {
            Network =
            [
                new("TWL", "UT", "TST", "3", "尖沙咀", "Tsim Sha Tsui", 1)
            ],
            Schedule = new MtrScheduleResponse
            {
                SystemTime = "2026-10-02 01:20:00",
                Status = 1,
                Data = new Dictionary<string, MtrStationSchedule>
                {
                    ["TWL-TST"] = new()
                }
            }
        };
        var provider = CreateProvider(
            client,
            DateTimeOffset.Parse("2026-10-02T01:20:00+08:00"));

        var result = await provider.GetMetroArrivalsAsync(
            "MTR:TWL:TST",
            CancellationToken.None);

        Assert.Empty(result.Data);
        Assert.Null(result.ServiceDayStatus);
        Assert.Null(result.LastDepartureAt);
        Assert.NotNull(result.Message);
        Assert.Equal(["3"], client.LastTrainScheduleRequests);
    }

    [Fact]
    public async Task RejectsUnknownStationWithoutExternalRequest()
    {
        var client = new StubMtrApiClient();
        var provider = CreateProvider(client);

        var result = await provider.GetMetroArrivalsAsync(
            "MTR:UNKNOWN:XXX",
            CancellationToken.None);

        Assert.Equal("unavailable", result.DataStatus);
        Assert.Empty(result.Data);
        Assert.Empty(client.ScheduleRequests);
    }

    private static MtrTransitProvider CreateProvider(
        StubMtrApiClient client,
        DateTimeOffset? now = null) =>
        new(
            client,
            new PassThroughProviderCache(),
            new FixedTimeProvider(now ?? Now),
            NullLogger<MtrTransitProvider>.Instance);

    private sealed class StubMtrApiClient : IMtrApiClient
    {
        public IReadOnlyList<MtrStationRow> Network { get; init; } = [];
        public MtrScheduleResponse Schedule { get; init; } = new();
        public IReadOnlyList<MtrLastTrainSchedule> LastTrainSchedules { get; init; } = [];
        public List<(string Line, string Station)> ScheduleRequests { get; } = [];
        public List<string> LastTrainScheduleRequests { get; } = [];

        public Task<MtrHttpResult<IReadOnlyList<MtrStationRow>>> GetLinesAndStationsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new MtrHttpResult<IReadOnlyList<MtrStationRow>>(Network, Now));

        public Task<MtrHttpResult<MtrScheduleResponse>> GetScheduleAsync(
            string lineCode,
            string stationCode,
            CancellationToken cancellationToken)
        {
            ScheduleRequests.Add((lineCode, stationCode));
            return Task.FromResult(new MtrHttpResult<MtrScheduleResponse>(Schedule, Now, Now));
        }

        public Task<MtrHttpResult<IReadOnlyList<MtrLastTrainSchedule>>> GetLastTrainSchedulesAsync(
            string stationId,
            CancellationToken cancellationToken)
        {
            LastTrainScheduleRequests.Add(stationId);
            return Task.FromResult(
                new MtrHttpResult<IReadOnlyList<MtrLastTrainSchedule>>(
                    LastTrainSchedules,
                    Now));
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
