namespace TravelInfoAssistant.Api.Contracts;

public sealed record CitySummaryResponse(
    Guid Id,
    string Code,
    string NameZh,
    string NameEn,
    string CountryCode,
    string TimeZone,
    double CenterLatitude,
    double CenterLongitude,
    IReadOnlyList<ServiceCapabilityResponse> Services);

public sealed record ServiceCapabilityResponse(
    string ServiceKey,
    string DisplayName,
    string IntegrationStatus,
    string AvailabilityStatus,
    string? Message);

public sealed record ResolveCityRequest(double Latitude, double Longitude);

public sealed record ResolveCityResponse(
    bool Supported,
    CitySummaryResponse? City,
    double? DistanceKilometers);
