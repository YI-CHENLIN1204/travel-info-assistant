using TravelInfoAssistant.Api.Providers.Hko;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class HkoAlertMapperTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-23T18:00:00+08:00");

    [Fact]
    public void Map_NormalizesAllCurrentWarningsTipsAndRecentFeltEarthquake()
    {
        var bundle = new HkoAlertBundle(
            WarningSummaryJson,
            WarningInfoJson,
            SpecialWeatherTipsJson,
            FeltEarthquakeJson,
            Now);

        var result = HkoAlertMapper.Map(bundle);

        Assert.Equal(5, result.Count);

        var rain = Assert.Single(result, alert => alert.Category == "暴雨警告信號");
        Assert.Equal("紅色暴雨警告信號", rain.Title);
        Assert.Equal("severe", rain.Severity);
        Assert.Equal("weather", rain.Kind);
        Assert.Contains("低窪地區", rain.Summary);

        var landslip = Assert.Single(result, alert => alert.Category == "山泥傾瀉警告");
        Assert.Equal("naturalHazard", landslip.Kind);
        Assert.Equal("severe", landslip.Severity);

        var thunderstorm = Assert.Single(result, alert => alert.Category == "雷暴警告");
        Assert.Equal("warning", thunderstorm.Severity);
        Assert.Equal(DateTimeOffset.Parse("2026-10-23T20:00:00+08:00"), thunderstorm.ExpiresAt);

        var tip = Assert.Single(result, alert => alert.Category == "特別天氣提示");
        Assert.Equal("severe", tip.Severity);
        Assert.Contains("八號", tip.Title);

        var earthquake = Assert.Single(result, alert => alert.Category == "本地有感地震");
        Assert.Equal("naturalHazard", earthquake.Kind);
        Assert.Equal("warning", earthquake.Severity);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T12:30:00+08:00"), earthquake.ExpiresAt);
        Assert.All(result, alert => Assert.Equal("香港天文台 HKO", alert.SourceName));
    }

    [Fact]
    public void Map_ExcludesCancelledWarningAndExpiredEarthquake()
    {
        const string cancelled = """
            {
              "WRAIN": {
                "name": "暴雨警告信號",
                "code": "WRAINR",
                "type": "紅色",
                "actionCode": "CANCEL",
                "issueTime": "2026-10-23T16:00:00+08:00",
                "updateTime": "2026-10-23T17:00:00+08:00"
              }
            }
            """;
        var oldEarthquake = FeltEarthquakeJson
            .Replace("2026-10-23T12:30:00+08:00", "2026-10-22T12:29:00+08:00")
            .Replace("2026-10-23T12:40:00+08:00", "2026-10-22T12:40:00+08:00");
        var bundle = new HkoAlertBundle(
            cancelled,
            WarningInfoJson,
            "{\"swt\":[]}",
            oldEarthquake,
            Now);

        Assert.Empty(HkoAlertMapper.Map(bundle));
    }

    [Fact]
    public void Map_MapsHighestOfficialSignalsToEmergency()
    {
        var summary = """
            {
              "WRAIN": {
                "name": "暴雨警告信號",
                "code": "WRAINB",
                "type": "黑色",
                "actionCode": "ISSUE",
                "issueTime": "2026-10-23T17:00:00+08:00",
                "updateTime": "2026-10-23T17:00:00+08:00"
              },
              "WTCSGNL": {
                "name": "熱帶氣旋警告信號",
                "code": "TC10",
                "type": "十號颶風信號",
                "actionCode": "ISSUE",
                "issueTime": "2026-10-23T17:05:00+08:00",
                "updateTime": "2026-10-23T17:05:00+08:00"
              }
            }
            """;
        var bundle = new HkoAlertBundle(summary, "{\"details\":[]}", "{}", "{}", Now);

        var result = HkoAlertMapper.Map(bundle);

        Assert.Equal(2, result.Count);
        Assert.All(result, alert => Assert.Equal("emergency", alert.Severity));
    }

    private const string WarningSummaryJson = """
        {
          "WRAIN": {
            "name": "暴雨警告信號",
            "code": "WRAINR",
            "type": "紅色",
            "actionCode": "ISSUE",
            "issueTime": "2026-10-23T16:00:00+08:00",
            "updateTime": "2026-10-23T16:15:00+08:00"
          },
          "WL": {
            "name": "山泥傾瀉警告",
            "code": "WL",
            "actionCode": "ISSUE",
            "issueTime": "2026-10-23T16:10:00+08:00",
            "updateTime": "2026-10-23T16:10:00+08:00"
          },
          "WTS": {
            "name": "雷暴警告",
            "code": "WTS",
            "actionCode": "EXTEND",
            "issueTime": "2026-10-23T16:30:00+08:00",
            "expireTime": "2026-10-23T20:00:00+08:00",
            "updateTime": "2026-10-23T17:00:00+08:00"
          }
        }
        """;

    private const string WarningInfoJson = """
        {
          "details": [
            {
              "contents": ["香港天文台發出紅色暴雨警告信號。", "低窪地區可能出現水浸。"],
              "warningStatementCode": "WRAIN",
              "subtype": "WRAINR",
              "updateTime": "2026-10-23T16:15:00+08:00"
            },
            {
              "contents": ["山泥傾瀉警告現正生效。"],
              "warningStatementCode": "WL",
              "updateTime": "2026-10-23T16:10:00+08:00"
            },
            {
              "contents": ["雷暴警告有效時間延長至晚上八時。"],
              "warningStatementCode": "WTS",
              "updateTime": "2026-10-23T17:00:00+08:00"
            }
          ]
        }
        """;

    private const string SpecialWeatherTipsJson = """
        {
          "swt": [
            {
              "desc": "香港天文台宣布預計在今晚八時前改發八號熱帶氣旋警告信號。市民應採取防風措施。",
              "updateTime": "2026-10-23T17:15:00+08:00"
            }
          ]
        }
        """;

    private const string FeltEarthquakeJson = """
        {
          "updateTime": "2026-10-23T12:40:00+08:00",
          "mag": 4.8,
          "region": "廣東沿岸",
          "intensity": "III（三）度",
          "details": "香港多區居民感到輕微震動。",
          "ptime": "2026-10-23T12:30:00+08:00"
        }
        """;
}
