namespace TravelInfoAssistant.Api.Providers.Odpt;

public sealed class OdptNotConfiguredException()
    : Exception("ODPT consumer key is not configured.");

public sealed class OdptProviderException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
