namespace TravelInfoAssistant.Api.Options;

public sealed class AeroDataBoxOptions
{
    public const string SectionName = "AeroDataBox";

    public string Gateway { get; set; } = "RapidApi";
    public string BaseUrl { get; set; } = "https://aerodatabox.p.rapidapi.com/";
    public string ApiKey { get; set; } = string.Empty;
    public string RapidApiHost { get; set; } = "aerodatabox.p.rapidapi.com";
    public int TimeoutSeconds { get; set; } = 15;
    public int RequestsPerSecond { get; set; } = 1;
    public int Tier2RequestUnits { get; set; } = 2;
    public long MonthlySoftLimitUnits { get; set; } = 360;
    public long MonthlyHardLimitUnits { get; set; } = 400;
    public long MonthlyTrafficSoftLimitBytes { get; set; } = 9_000_000_000;
    public int BillingCycleDay { get; set; } = 1;
    public bool AllowPaidOverage { get; set; }
    public string PricingVerifiedAt { get; set; } = "2026-09-23";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public bool IsDirect =>
        string.Equals(Gateway, "Direct", StringComparison.OrdinalIgnoreCase);
}
