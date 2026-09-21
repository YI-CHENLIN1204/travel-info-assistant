namespace TravelInfoAssistant.Api.Providers.Tdx;

public sealed class TdxNotConfiguredException()
    : Exception("TDX credentials are not configured.");

public sealed class TdxQuotaExceededException()
    : Exception("The internal TDX monthly soft limit has been reached.");

public sealed class TdxRateLimitException()
    : Exception("The internal TDX per-minute rate limit has been reached.");

public sealed class TdxProviderException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
