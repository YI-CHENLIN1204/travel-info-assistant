namespace TravelInfoAssistant.Api.Options;

public sealed class TdxOptions
{
    public const string SectionName = "Tdx";

    public string BaseUrl { get; set; } = "https://tdx.transportdata.tw/api/basic/";
    public string TokenUrl { get; set; } =
        "https://tdx.transportdata.tw/auth/realms/TDXConnect/protocol/openid-connect/token";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 12;
    public int RequestsPerMinute { get; set; } = 4;
    public double MonthlySoftLimitPoints { get; set; } = 2.7;
    public double MonthlyHardLimitPoints { get; set; } = 3.0;
    public int RequestsPerPoint { get; set; } = 1500;
    public int MegabytesPerPoint { get; set; } = 150;
    public bool AllowPaidOverage { get; set; }
    public string PricingVerifiedAt { get; set; } = "2026-09-21";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
