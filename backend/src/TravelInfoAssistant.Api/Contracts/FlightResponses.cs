namespace TravelInfoAssistant.Api.Contracts;

public sealed record AirportResponse(
    string Iata,
    string? Icao,
    string Name,
    string? Municipality,
    string? CountryCode,
    double? Latitude,
    double? Longitude,
    string? TimeZone = null);

public sealed record FlightTimeResponse(
    string? Local,
    DateTimeOffset? Utc);

public sealed record FlightMovementResponse(
    AirportResponse Airport,
    FlightTimeResponse? Scheduled,
    FlightTimeResponse? Estimated,
    FlightTimeResponse? Actual,
    string? Terminal,
    string? Gate);

public sealed record FlightAirlineResponse(
    string Name,
    string? Iata,
    string? Icao);

public sealed record FlightAircraftResponse(
    string? Registration,
    string? Model);

public sealed record FlightSegmentResponse(
    string Id,
    string FlightNumber,
    string? CallSign,
    string Status,
    FlightAirlineResponse? Airline,
    FlightAircraftResponse? Aircraft,
    FlightMovementResponse Departure,
    FlightMovementResponse Arrival,
    DateTimeOffset? SourceUpdatedAt);

public sealed record FlightItineraryResponse(
    string Id,
    int Stops,
    IReadOnlyList<FlightSegmentResponse> Segments);

public sealed record AeroDataBoxProviderStatusResponse(
    bool Configured,
    string Gateway,
    string BillingCycle,
    long RequestCount,
    long UsedUnits,
    long SoftLimitUnits,
    long HardLimitUnits,
    long ResponseBytes,
    long TrafficSoftLimitBytes,
    int RequestsPerSecond,
    bool OverageEnabled,
    string PricingVerifiedAt);
