namespace TravelInfoAssistant.Api.Contracts;

public sealed record EmergencyInfoResponse(
    string LocationName,
    string CountryCode,
    IReadOnlyList<EmergencyContactResponse> Contacts,
    OverseasOfficeResponse? OverseasOffice,
    IReadOnlyList<EmergencyGuideResponse> Guides,
    DateOnly LastVerifiedOn);

public sealed record EmergencyContactResponse(
    Guid Id,
    string Category,
    string DisplayName,
    string PhoneNumber,
    string? Note,
    string SourceName,
    string SourceUrl,
    DateOnly VerifiedOn);

public sealed record OverseasOfficeResponse(
    Guid Id,
    string NameZh,
    string Address,
    string MainPhone,
    string EmergencyPhone,
    string? Note,
    string SourceName,
    string SourceUrl,
    DateOnly VerifiedOn);

public sealed record EmergencyGuideResponse(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    IReadOnlyList<string> Steps,
    string SourceName,
    string SourceUrl,
    DateOnly VerifiedOn);
