using TravelInfoAssistant.Api.Providers.Boca;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class BocaAlertMapperTests
{
    [Fact]
    public void Map_ParsesOfficialRssFieldsAndRemovesHtml()
    {
        const string xml = """
            <rss><channel><item>
              <title><![CDATA[第一級：灰色提醒 - 日本 - Japan]]></title>
              <link>https://www.boca.gov.tw/sp-trwa-content-2137-338f3-1.html</link>
              <description><![CDATA[<p>請注意自身安全。<br />並購買旅遊保險。</p>]]></description>
              <pubDate>Thu, 24 Sep 2026 01:00:00 GMT</pubDate>
              <NewsID>2137</NewsID>
            </item></channel></rss>
            """;

        var result = BocaAlertMapper.Map(xml);

        var alert = Assert.Single(result);
        Assert.Equal("2137", alert.Id);
        Assert.Equal(1, alert.Level);
        Assert.Equal("JP", alert.CountryCode);
        Assert.Equal("日本", alert.RegionName);
        Assert.Equal("請注意自身安全。 並購買旅遊保險。", alert.Summary);
        Assert.Equal(DateTimeOffset.Parse("2026-09-24T01:00:00Z"), alert.UpdatedAt);
    }

    [Fact]
    public void Map_OrdersHighestLevelFirstAndMapsPopularCountries()
    {
        const string xml = """
            <rss><channel>
              <item><title><![CDATA[第二級：黃色注意 - 韓國 - Korea (South Korea)]]></title>
                <link>https://example.com/kr</link><description>韓國提醒</description><NewsID>2</NewsID></item>
              <item><title><![CDATA[第三級：橙色避免前往 - 泰國邊境 - Thailand]]></title>
                <link>https://example.com/th</link><description>泰國提醒</description><NewsID>3</NewsID></item>
            </channel></rss>
            """;

        var result = BocaAlertMapper.Map(xml);

        Assert.Equal([3, 2], result.Select(item => item.Level));
        Assert.Equal("TH", result[0].CountryCode);
        Assert.Equal("KR", result[1].CountryCode);
    }
}
