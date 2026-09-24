namespace TravelInfoAssistant.Api.Providers.Boca;

public sealed class BocaProviderException(string message, int? statusCode = null)
    : Exception(message)
{
    public int? StatusCode { get; } = statusCode;
}
