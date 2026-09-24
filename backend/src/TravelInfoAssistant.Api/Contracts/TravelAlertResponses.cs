namespace TravelInfoAssistant.Api.Contracts;

public sealed record TravelAlertResponse(
    string Id,
    int Level,
    string LevelLabel,
    string CountryCode,
    string CountryNameZh,
    string CountryNameEn,
    string RegionName,
    string Summary,
    DateTimeOffset? UpdatedAt,
    string SourceUrl);
