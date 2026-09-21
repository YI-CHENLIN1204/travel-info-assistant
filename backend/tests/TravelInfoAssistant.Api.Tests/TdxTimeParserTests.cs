using TravelInfoAssistant.Api.Providers.Tdx;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class TdxTimeParserTests
{
    [Fact]
    public void ParseNextOccurrence_RollsPastMidnightInTaipei()
    {
        var reference = DateTimeOffset.Parse("2026-09-21T15:58:00Z");

        var result = TdxTimeParser.ParseNextOccurrence("00:05", reference);

        Assert.Equal(DateTimeOffset.Parse("2026-09-21T16:05:00Z"), result);
    }

    [Fact]
    public void ParseNextOccurrence_ReturnsNullForMissingTime()
    {
        Assert.Null(TdxTimeParser.ParseNextOccurrence(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsServiceDay_UsesTaipeiLocalWeekday()
    {
        var sundayUtcButMondayInTaipei = DateTimeOffset.Parse("2026-09-20T16:30:00Z");
        var serviceDay = new TdxServiceDay { Monday = true };

        var result = TdxTimeParser.IsServiceDay(serviceDay, sundayUtcButMondayInTaipei);

        Assert.True(result);
    }
}
