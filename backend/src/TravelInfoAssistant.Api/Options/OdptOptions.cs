namespace TravelInfoAssistant.Api.Options;

public sealed class OdptOptions
{
    public const string SectionName = "Odpt";

    public string BaseUrl { get; set; } = "https://api.odpt.org/api/v4/";
    public string ConsumerKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 12;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConsumerKey);
}
