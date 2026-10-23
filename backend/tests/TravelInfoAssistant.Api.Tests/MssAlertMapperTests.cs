using TravelInfoAssistant.Api.Providers.Mss;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class MssAlertMapperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T17:00:00+08:00");

    [Fact]
    public void Map_UsesSingaporeValidityAndExcludesExpiredWarning()
    {
        var xml = Feed("Heavy thundery showers from 1630 to 1830 hrs.");
        var alert = Assert.Single(MssAlertMapper.Map(xml, Now));
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T18:30:00+08:00"), alert.ExpiresAt);
        Assert.Empty(MssAlertMapper.Map(xml, Now.AddHours(2)));
    }

    [Theory]
    [InlineData("NIL")]
    [InlineData("No heavy rain warning")]
    [InlineData("Warning cancelled")]
    public void Map_DoesNotTurnClearOrCancelledBulletinIntoAlert(string summary) =>
        Assert.Empty(MssAlertMapper.Map(Feed(summary), Now));

    [Fact]
    public void Map_UnknownValidityIsFailureRatherThanFalseAllClear() =>
        Assert.Throws<FormatException>(() => MssAlertMapper.Map(Feed("Heavy rain expected."), Now));

    private static string Feed(string summary) => $$"""
        <feed xmlns="http://www.w3.org/2005/Atom"><entry>
        <id>mss-test</id><updated>2026-10-24T08:30:00Z</updated>
        <summary>{{summary}}</summary></entry></feed>
        """;
}
