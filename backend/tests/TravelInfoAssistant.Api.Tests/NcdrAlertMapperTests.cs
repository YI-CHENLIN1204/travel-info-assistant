using TravelInfoAssistant.Api.Providers.Ncdr;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class NcdrAlertMapperTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-23T21:00:00+08:00");

    [Fact]
    public void Map_ParsesAllActiveCategoriesAndOfficialTimes()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:cap="urn:oasis:names:tc:emergency:cap:1.1">
              <entry>
                <id>CWA-rain-1</id>
                <title>降雨</title>
                <updated>2026-10-23T17:09:58+08:00</updated>
                <author><name>中央氣象署</name></author>
                <link rel="alternate" href="https://alerts.ncdr.nat.gov.tw/rain.cap" />
                <summary type="html"><![CDATA[<p>臺北市山區有局部豪雨，請注意。</p>]]></summary>
                <category term="降雨" />
                <cap:status>Actual</cap:status>
                <cap:msgType>Update</cap:msgType>
                <cap:effective>2026/10/23 下午 05:05:00</cap:effective>
                <cap:expires>2026/10/24 下午 11:00:00</cap:expires>
              </entry>
              <entry>
                <id>THB-road-1</id>
                <title>道路封閉</title>
                <updated>2026-10-23T20:49:04+08:00</updated>
                <author><name>交通部公路局</name></author>
                <link rel="alternate" href="https://alerts.ncdr.nat.gov.tw/road.cap" />
                <summary>道路預警性封閉。</summary>
                <category term="道路封閉" />
                <cap:status>Actual</cap:status>
                <cap:msgType>Alert</cap:msgType>
                <cap:effective>2026/10/23 下午 07:00:00</cap:effective>
                <cap:expires>2026/10/24 下午 11:52:00</cap:expires>
              </entry>
            </feed>
            """;

        var result = NcdrAlertMapper.Map(xml, "臺北市", Now);

        Assert.Equal(2, result.Count);
        Assert.Equal("transport", result[0].Kind);
        Assert.Equal("道路封閉", result[0].Category);
        Assert.Equal("weather", result[1].Kind);
        Assert.Equal("臺北市山區有局部豪雨，請注意。", result[1].Summary);
        Assert.Equal("官方生效中", result[1].SeverityLabel);
        Assert.Equal(DateTimeOffset.Parse("2026-10-23T17:05:00+08:00"), result[1].EffectiveAt);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T23:00:00+08:00"), result[1].ExpiresAt);
        Assert.Equal("中央氣象署", result[1].SourceName);
    }

    [Fact]
    public void Map_RemovesCancelledExpiredAndNonActualEntries()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:cap="urn:oasis:names:tc:emergency:cap:1.1">
              <entry>
                <id>cancelled</id><title>道路封閉</title><updated>2026-10-23T20:00:00+08:00</updated>
                <author><name>交通部公路局</name></author>
                <link rel="alternate" href="https://example.com/cancelled" />
                <summary>取消封閉。</summary><category term="道路封閉" />
                <cap:status>Actual</cap:status><cap:msgType>Cancel</cap:msgType>
                <cap:expires>2026/10/24 下午 11:00:00</cap:expires>
              </entry>
              <entry>
                <id>expired</id><title>雷雨</title><updated>2026-10-23T18:00:00+08:00</updated>
                <author><name>中央氣象署</name></author>
                <link rel="alternate" href="https://example.com/expired" />
                <summary>已經過期。</summary><category term="雷雨" />
                <cap:status>Actual</cap:status><cap:msgType>Alert</cap:msgType>
                <cap:expires>2026/10/23 下午 08:00:00</cap:expires>
              </entry>
              <entry>
                <id>test</id><title>地震</title><updated>2026-10-23T20:00:00+08:00</updated>
                <author><name>測試單位</name></author>
                <link rel="alternate" href="https://example.com/test" />
                <summary>測試警報。</summary><category term="地震" />
                <cap:status>Test</cap:status><cap:msgType>Alert</cap:msgType>
                <cap:expires>2026/10/24 下午 11:00:00</cap:expires>
              </entry>
            </feed>
            """;

        Assert.Empty(NcdrAlertMapper.Map(xml, "臺北市", Now));
    }

    [Fact]
    public void Map_CreatesStableUniqueIdsForAreaSpecificEntries()
    {
        const string xml = """
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:cap="urn:oasis:names:tc:emergency:cap:1.1">
              <entry>
                <id>same-id</id><title>土石流及大規模崩塌</title><updated>2026-10-23T18:30:00+08:00</updated>
                <author><name>水土保持署</name></author><link rel="alternate" href="https://example.com/a" />
                <summary>第一處警戒。</summary><category term="土石流及大規模崩塌" />
                <cap:status>Actual</cap:status><cap:msgType>Alert</cap:msgType>
              </entry>
              <entry>
                <id>same-id</id><title>土石流及大規模崩塌</title><updated>2026-10-23T18:30:00+08:00</updated>
                <author><name>水土保持署</name></author><link rel="alternate" href="https://example.com/a" />
                <summary>第二處警戒。</summary><category term="土石流及大規模崩塌" />
                <cap:status>Actual</cap:status><cap:msgType>Alert</cap:msgType>
              </entry>
            </feed>
            """;

        var result = NcdrAlertMapper.Map(xml, "臺北市", Now);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Select(alert => alert.Id).Distinct().Count());
        Assert.All(result, alert => Assert.Equal("naturalHazard", alert.Kind));
    }
}
