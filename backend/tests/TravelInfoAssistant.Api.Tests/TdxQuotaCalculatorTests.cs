using TravelInfoAssistant.Api.Providers.Tdx;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class TdxQuotaCalculatorTests
{
    [Fact]
    public void EstimatePoints_AddsRequestAndTrafficPoints()
    {
        var responseBytes = 150L * 1024 * 1024;

        var points = TdxQuotaCalculator.EstimatePoints(
            1500,
            responseBytes,
            requestsPerPoint: 1500,
            megabytesPerPoint: 150);

        Assert.Equal(2, points, 6);
    }

    [Fact]
    public void EstimatePoints_SupportsPartialUsage()
    {
        var responseBytes = 75L * 1024 * 1024;

        var points = TdxQuotaCalculator.EstimatePoints(
            750,
            responseBytes,
            requestsPerPoint: 1500,
            megabytesPerPoint: 150);

        Assert.Equal(1, points, 6);
    }

    [Fact]
    public void EstimatePoints_RejectsInvalidConversionValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TdxQuotaCalculator.EstimatePoints(1, 1, 0, 150));
    }
}
