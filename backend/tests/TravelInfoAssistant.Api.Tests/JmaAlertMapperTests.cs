using TravelInfoAssistant.Api.Providers.Jma;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class JmaAlertMapperTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-10-23T21:00:00+09:00");

    [Fact]
    public void Map_NormalizesTokyoWeatherEarthquakeTsunamiAndVolcanoReports()
    {
        var bundle = new JmaDocumentBundle(
        [
            Bulletin(WeatherXml, "weather.xml", "2026-10-23T20:45:00+09:00"),
            Bulletin(EarthquakeXml, "earthquake.xml", "2026-10-23T19:30:00+09:00"),
            Bulletin(TsunamiXml, "tsunami.xml", "2026-10-23T18:00:00+09:00"),
            Bulletin(VolcanoXml, "volcano.xml", "2026-10-23T17:00:00+09:00")
        ],
        Now);

        var result = JmaAlertMapper.Map(bundle, "東京都");

        Assert.Equal(4, result.Count);
        var weather = Assert.Single(result, alert => alert.Category == "氣象警報・注意報");
        Assert.Equal("weather", weather.Kind);
        Assert.Equal("severe", weather.Severity);
        Assert.Equal("暴風警報、雷雨注意報", weather.Title);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T03:00:00+09:00"), weather.ExpiresAt);

        var earthquake = Assert.Single(result, alert => alert.Category == "地震資訊");
        Assert.Equal("東京都觀測到震度5弱", earthquake.Title);
        Assert.Equal("severe", earthquake.Severity);
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T19:30:00+09:00"), earthquake.ExpiresAt);

        var tsunami = Assert.Single(result, alert => alert.Category == "海嘯警報・注意報");
        Assert.Equal("海嘯注意報", tsunami.Title);
        Assert.Equal("warning", tsunami.Severity);

        var volcano = Assert.Single(result, alert => alert.Category == "火山警報・資訊");
        Assert.Contains("噴火警報", volcano.Title);
        Assert.Equal("severe", volcano.Severity);
        Assert.All(result, alert => Assert.StartsWith("日本氣象廳 JMA", alert.SourceName));
    }

    [Fact]
    public void Map_LatestCancellationClearsWeatherAndOldEarthquakeExpires()
    {
        var cancelledWeather = WeatherXml
            .Replace("<InfoType>発表</InfoType>", "<InfoType>取消</InfoType>")
            .Replace("20:45:00", "20:55:00");
        var oldEarthquake = EarthquakeXml.Replace("2026-10-23T19:30:00+09:00", "2026-10-22T18:00:00+09:00");
        var bundle = new JmaDocumentBundle(
        [
            Bulletin(WeatherXml, "weather-old.xml", "2026-10-23T20:45:00+09:00"),
            Bulletin(cancelledWeather, "weather-cancel.xml", "2026-10-23T20:55:00+09:00"),
            Bulletin(oldEarthquake, "earthquake-old.xml", "2026-10-22T18:00:00+09:00")
        ],
        Now);

        Assert.Empty(JmaAlertMapper.Map(bundle, "東京都"));
    }

    [Fact]
    public void Map_DoesNotPromoteMinorTokyoEarthquakeToTravelAlert()
    {
        var minorEarthquake = EarthquakeXml.Replace("<MaxInt>5-</MaxInt>", "<MaxInt>2</MaxInt>");
        var bundle = new JmaDocumentBundle(
            [Bulletin(minorEarthquake, "minor-earthquake.xml", "2026-10-23T19:30:00+09:00")],
            Now);

        Assert.Empty(JmaAlertMapper.Map(bundle, "東京都"));
    }

    private static JmaBulletinDocument Bulletin(string xml, string name, string updatedAt) =>
        new(
            xml,
            $"https://www.data.jma.go.jp/developer/xml/data/{name}",
            DateTimeOffset.Parse(updatedAt));

    private const string WeatherXml = """
        <Report xmlns="http://xml.kishou.go.jp/jmaxml1/">
          <Control><PublishingOffice>気象庁</PublishingOffice></Control>
          <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
            <Title>東京都気象警報・注意報</Title>
            <ReportDateTime>2026-10-23T20:45:00+09:00</ReportDateTime>
            <TargetDateTime>2026-10-23T20:45:00+09:00</TargetDateTime>
            <ValidDateTime>2026-10-24T03:00:00+09:00</ValidDateTime>
            <EventID>tokyo-weather</EventID><InfoType>発表</InfoType>
            <InfoKind>気象警報・注意報</InfoKind>
            <Headline><Text>東京都では暴風に警戒し、落雷に注意してください。</Text></Headline>
          </Head>
          <Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/meteorology1/">
            <Warning><Item>
              <Kind><Name>暴風警報</Name><Status>発表</Status></Kind>
              <Kind><Name>雷注意報</Name><Status>継続</Status></Kind>
              <Area><Name>東京都</Name><Code>130000</Code></Area>
            </Item></Warning>
          </Body>
        </Report>
        """;

    private const string EarthquakeXml = """
        <Report xmlns="http://xml.kishou.go.jp/jmaxml1/">
          <Control><PublishingOffice>気象庁</PublishingOffice></Control>
          <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
            <Title>震源・震度情報</Title>
            <ReportDateTime>2026-10-23T19:30:00+09:00</ReportDateTime>
            <TargetDateTime>2026-10-23T19:28:00+09:00</TargetDateTime>
            <EventID>quake-1</EventID><InfoType>発表</InfoType>
            <InfoKind>震源・震度に関する情報</InfoKind>
            <Headline><Text>２３日１９時２８分ころ、地震がありました。</Text></Headline>
          </Head>
          <Body xmlns="http://xml.kishou.go.jp/jmaxml1/body/seismology1/">
            <Intensity><Observation><Pref>
              <Name>東京都</Name><Code>13</Code><MaxInt>5-</MaxInt>
            </Pref></Observation></Intensity>
          </Body>
        </Report>
        """;

    private const string TsunamiXml = """
        <Report xmlns="http://xml.kishou.go.jp/jmaxml1/">
          <Control><PublishingOffice>気象庁</PublishingOffice></Control>
          <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
            <Title>津波警報・注意報・予報</Title>
            <ReportDateTime>2026-10-23T18:00:00+09:00</ReportDateTime>
            <EventID>tsunami-1</EventID><InfoType>発表</InfoType>
            <InfoKind>津波警報・注意報・予報</InfoKind>
            <Headline><Text>東京湾内湾に津波注意報を発表しました。</Text></Headline>
            <Information><Item><Kind><Name>津波注意報</Name></Kind><Areas><Area><Name>東京湾内湾</Name></Area></Areas></Item></Information>
          </Head>
        </Report>
        """;

    private const string VolcanoXml = """
        <Report xmlns="http://xml.kishou.go.jp/jmaxml1/">
          <Control><PublishingOffice>気象庁</PublishingOffice></Control>
          <Head xmlns="http://xml.kishou.go.jp/jmaxml1/informationBasis1/">
            <Title>硫黄島 噴火警報</Title>
            <ReportDateTime>2026-10-23T17:00:00+09:00</ReportDateTime>
            <EventID>volcano-1</EventID><InfoType>発表</InfoType>
            <InfoKind>噴火警報・予報</InfoKind>
            <Headline><Text>硫黄島で噴火警報を発表しました。</Text></Headline>
            <Information><Item><Kind><Name>噴火警報（居住地域）</Name></Kind><Areas><Area><Name>硫黄島</Name></Area></Areas></Item></Information>
          </Head>
        </Report>
        """;
}
