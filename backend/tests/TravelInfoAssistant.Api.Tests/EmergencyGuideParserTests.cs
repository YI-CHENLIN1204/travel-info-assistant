using TravelInfoAssistant.Api.Services.Emergency;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class EmergencyGuideParserTests
{
    [Fact]
    public void ParseSteps_ReturnsOrderedSteps()
    {
        var result = EmergencyGuideParser.ParseSteps("[\"先報警\",\"取得證明\"]");

        Assert.Equal(["先報警", "取得證明"], result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("null")]
    public void ParseSteps_InvalidPayload_ReturnsEmpty(string value)
    {
        Assert.Empty(EmergencyGuideParser.ParseSteps(value));
    }
}
