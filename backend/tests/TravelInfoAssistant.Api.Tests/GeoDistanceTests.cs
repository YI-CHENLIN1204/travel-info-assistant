using TravelInfoAssistant.Api.Services;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class GeoDistanceTests
{
    [Fact]
    public void SameCoordinate_ReturnsZero()
    {
        var distance = GeoDistance.CalculateKilometers(25.0375, 121.5637, 25.0375, 121.5637);

        Assert.Equal(0, distance, 6);
    }

    [Fact]
    public void TaipeiToTokyo_ReturnsExpectedApproximateDistance()
    {
        var distance = GeoDistance.CalculateKilometers(25.0375, 121.5637, 35.6762, 139.6503);

        Assert.InRange(distance, 2050, 2150);
    }
}
