namespace TravelInfoAssistant.Api.Providers.Tdx;

public static class TdxQuotaCalculator
{
    public static double EstimatePoints(
        long requestCount,
        long responseBytes,
        int requestsPerPoint,
        int megabytesPerPoint)
    {
        if (requestsPerPoint <= 0 || megabytesPerPoint <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requestsPerPoint),
                "TDX point conversion values must be greater than zero.");
        }

        var requestPoints = requestCount / (double)requestsPerPoint;
        var bytePoints = responseBytes / (megabytesPerPoint * 1024d * 1024d);
        return requestPoints + bytePoints;
    }
}
