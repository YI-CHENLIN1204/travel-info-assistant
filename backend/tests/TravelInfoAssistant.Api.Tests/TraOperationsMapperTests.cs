using TravelInfoAssistant.Api.Providers.Tra;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class TraOperationsMapperTests
{
    [Fact]
    public void Map_CombinesDisruptedLinesAndSkipsNormalLines()
    {
        var html = Block("西部幹線", "全線正常運行", "") +
            Block("平溪線", "部份受阻", Row("三貂嶺-菁桐")) +
            Block("深澳線", "部份受阻", Row("八斗子-瑞芳"));
        var alert = Assert.Single(TraOperationsMapper.Map(html));
        Assert.Equal("transport", alert.Kind);
        Assert.Contains("平溪線", alert.RegionName);
        Assert.Contains("深澳線", alert.RegionName);
        Assert.DoesNotContain("西部幹線", alert.Summary);
        Assert.Contains("停駛", alert.Summary);
        Assert.Equal(DateTimeOffset.Parse("2026-10-23T06:11:00+08:00"), alert.UpdatedAt);
        Assert.Null(alert.ExpiresAt); // Restoration is not inferred from an estimated time.
    }

    [Fact]
    public void Map_CurrentNormalStatusClearsPreviousDisruption() =>
        Assert.Empty(TraOperationsMapper.Map(Block("平溪線", "全線正常運行", "")));

    [Fact]
    public void Map_UnknownHtmlFailsInsteadOfClaimingAllClear() =>
        Assert.Throws<FormatException>(() => TraOperationsMapper.Map("<html>維護中</html>"));

    [Fact]
    public void Map_MissingDisruptionDetailsFails() =>
        Assert.Throws<FormatException>(() => TraOperationsMapper.Map(Block("平溪線", "部份受阻", "")));

    private static string Block(string line, string status, string rows) =>
        $"<dl class=\"now_status\"><dt>{line}</dt><label>{status}</label></dl><table class=\"itinerary-controls tdbg\">{rows}</table>";

    private static string Row(string section) => $"""
        <tr><td width="13%">2026/10/23 06:11</td><td width="15%">
        <!--起迄站說明--><span class="icon-fa fullcircle"></span><label>{section}</label></td>
        <td width="53%">因應颱風，平溪線、深澳線全區間停駛。</td><td width="19%">預計恢復時間:評估中</td></tr>
        """;
}
