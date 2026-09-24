namespace TravelInfoAssistant.Api.Providers.MetNorway;

public sealed class MetNorwayProviderException(
    string message,
    int? statusCode = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
