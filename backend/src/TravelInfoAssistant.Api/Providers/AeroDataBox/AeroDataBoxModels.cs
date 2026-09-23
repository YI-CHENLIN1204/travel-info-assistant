namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public sealed class AeroDataBoxFidsResponse
{
    public IReadOnlyList<AeroDataBoxFlight> Departures { get; init; } = [];
    public IReadOnlyList<AeroDataBoxFlight> Arrivals { get; init; } = [];
}

public sealed class AeroDataBoxFlight
{
    public AeroDataBoxMovement? Movement { get; init; }
    public AeroDataBoxMovement? Departure { get; init; }
    public AeroDataBoxMovement? Arrival { get; init; }
    public string? Number { get; init; }
    public string? CallSign { get; init; }
    public string? Status { get; init; }
    public string? CodeshareStatus { get; init; }
    public bool IsCargo { get; init; }
    public AeroDataBoxAirline? Airline { get; init; }
    public AeroDataBoxAircraft? Aircraft { get; init; }
    public DateTimeOffset? LastUpdatedUtc { get; init; }
}

public sealed class AeroDataBoxMovement
{
    public AeroDataBoxAirport? Airport { get; init; }
    public AeroDataBoxDateTime? ScheduledTime { get; init; }
    public AeroDataBoxDateTime? RevisedTime { get; init; }
    public AeroDataBoxDateTime? PredictedTime { get; init; }
    public AeroDataBoxDateTime? RunwayTime { get; init; }
    public string? Terminal { get; init; }
    public string? Gate { get; init; }
    public IReadOnlyList<string> Quality { get; init; } = [];
}

public sealed class AeroDataBoxDateTime
{
    public string? Utc { get; init; }
    public string? Local { get; init; }
}

public sealed class AeroDataBoxAirport
{
    public string? Iata { get; init; }
    public string? Icao { get; init; }
    public string? Name { get; init; }
    public string? ShortName { get; init; }
    public string? MunicipalityName { get; init; }
    public string? CountryCode { get; init; }
    public string? TimeZone { get; init; }
}

public sealed class AeroDataBoxAirline
{
    public string? Name { get; init; }
    public string? Iata { get; init; }
    public string? Icao { get; init; }
}

public sealed class AeroDataBoxAircraft
{
    public string? Reg { get; init; }
    public string? Model { get; init; }
}
