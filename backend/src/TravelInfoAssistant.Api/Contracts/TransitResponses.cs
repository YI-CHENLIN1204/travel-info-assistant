namespace TravelInfoAssistant.Api.Contracts;

public sealed record TransitModeResponse(
    string Key,
    string DisplayName,
    string AvailabilityStatus,
    string? Message);

public sealed record TransitDirectionResponse(
    int Direction,
    string? Headsign,
    string? OriginName,
    string? DestinationName);

public sealed record TransitRouteResponse(
    string Id,
    string NameZh,
    string? NameEn,
    string? OriginName,
    string? DestinationName,
    IReadOnlyList<string> Operators,
    IReadOnlyList<TransitDirectionResponse> Directions);

public sealed record TransitStopResponse(
    string Id,
    string NameZh,
    string? NameEn,
    int Sequence,
    int Direction,
    double? Latitude,
    double? Longitude);

public sealed record MetroStationResponse(
    string Id,
    string NameZh,
    string? NameEn,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? Code = null,
    string? RailwayId = null,
    string? RailwayName = null);

public sealed record RailStationResponse(
    string Id,
    string NameZh,
    string? NameEn,
    string? Address,
    double? Latitude,
    double? Longitude);

public sealed record TransitArrivalResponse(
    string Id,
    string Mode,
    string StopId,
    string StopName,
    string? RouteId,
    string? RouteName,
    string? LineId,
    string? LineName,
    string? DestinationName,
    int? Direction,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? EstimatedAt,
    DateTimeOffset? SourceUpdatedAt,
    string ServiceStatus,
    bool IsLastService,
    string? Platform = null);

public sealed record TdxProviderStatusResponse(
    bool Configured,
    string BillingCycle,
    long RequestCount,
    long ResponseBytes,
    double EstimatedPoints,
    double SoftLimitPoints,
    double HardLimitPoints,
    int RequestsPerMinute,
    bool OverageEnabled,
    string PricingVerifiedAt);
