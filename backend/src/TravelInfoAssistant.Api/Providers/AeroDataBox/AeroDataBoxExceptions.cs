namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public sealed class AeroDataBoxNotConfiguredException()
    : Exception("AeroDataBox credentials are not configured.");

public sealed class AeroDataBoxQuotaExceededException()
    : Exception("The internal AeroDataBox usage limit has been reached.");

public sealed class AeroDataBoxProviderException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
