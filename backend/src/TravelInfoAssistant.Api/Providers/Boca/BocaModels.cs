namespace TravelInfoAssistant.Api.Providers.Boca;

public sealed record BocaRssDocument(
    string Xml,
    DateTimeOffset FetchedAt,
    DateTimeOffset? LastModified);
