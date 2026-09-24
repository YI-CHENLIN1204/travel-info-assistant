using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Providers.AeroDataBox;

public static class AeroDataBoxFlightMapper
{
    private static readonly HashSet<string> DepartedStatuses = new(
        ["Departed", "EnRoute", "Approaching", "Arrived", "Diverted"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ArrivedStatuses = new(
        ["Arrived", "Diverted"],
        StringComparer.OrdinalIgnoreCase);

    public static FlightSegmentResponse? MapFidsFlight(
        AeroDataBoxFlight flight,
        AirportResponse origin)
    {
        if (string.IsNullOrWhiteSpace(flight.Number))
        {
            return null;
        }

        var departureSource = flight.Departure ?? flight.Movement;
        var arrivalSource = flight.Arrival;
        if (arrivalSource is null && flight.Movement?.Airport is not null)
        {
            arrivalSource = new AeroDataBoxMovement { Airport = flight.Movement.Airport };
        }

        var arrivalAirport = MapAirport(arrivalSource?.Airport, null);
        if (arrivalAirport is null)
        {
            return null;
        }

        return Map(
            flight,
            MapMovement(departureSource, origin, flight.Status, false),
            MapMovement(arrivalSource, arrivalAirport, flight.Status, true));
    }

    public static FlightSegmentResponse? MapFlight(AeroDataBoxFlight flight)
    {
        if (string.IsNullOrWhiteSpace(flight.Number))
        {
            return null;
        }

        var departureAirport = MapAirport(flight.Departure?.Airport, null);
        var arrivalAirport = MapAirport(flight.Arrival?.Airport, null);
        if (departureAirport is null || arrivalAirport is null)
        {
            return null;
        }

        return Map(
            flight,
            MapMovement(flight.Departure, departureAirport, flight.Status, false),
            MapMovement(flight.Arrival, arrivalAirport, flight.Status, true));
    }

    public static bool HasLiveData(AeroDataBoxFlight flight) =>
        HasLiveQuality(flight.Departure) ||
        HasLiveQuality(flight.Arrival) ||
        HasLiveQuality(flight.Movement);

    public static DateTimeOffset? GetLastUpdatedUtc(AeroDataBoxFlight flight) =>
        ParseUtcTimestamp(flight.LastUpdatedUtc);

    private static FlightSegmentResponse Map(
        AeroDataBoxFlight flight,
        FlightMovementResponse departure,
        FlightMovementResponse arrival)
    {
        var status = string.IsNullOrWhiteSpace(flight.Status) ? "Unknown" : flight.Status;
        var airline = string.IsNullOrWhiteSpace(flight.Airline?.Name)
            ? null
            : new FlightAirlineResponse(
                flight.Airline.Name,
                flight.Airline.Iata,
                flight.Airline.Icao);
        var aircraft = flight.Aircraft is null
            ? null
            : new FlightAircraftResponse(flight.Aircraft.Reg, flight.Aircraft.Model);
        var identity = string.Join(
            '|',
            flight.Number,
            departure.Airport.Iata,
            arrival.Airport.Iata,
            departure.Scheduled?.Utc?.ToString("O", CultureInfo.InvariantCulture)
                ?? departure.Scheduled?.Local);

        return new FlightSegmentResponse(
            $"flight:{Hash(identity)}",
            flight.Number!.Trim().ToUpperInvariant(),
            flight.CallSign,
            status,
            airline,
            aircraft,
            departure,
            arrival,
            GetLastUpdatedUtc(flight));
    }

    private static FlightMovementResponse MapMovement(
        AeroDataBoxMovement? movement,
        AirportResponse airport,
        string? status,
        bool arrival)
    {
        var completed = arrival
            ? ArrivedStatuses.Contains(status ?? string.Empty)
            : DepartedStatuses.Contains(status ?? string.Empty);
        var revised = movement?.RevisedTime ?? movement?.RunwayTime;
        var predicted = movement?.PredictedTime;

        return new FlightMovementResponse(
            MapAirport(movement?.Airport, airport) ?? airport,
            MapTime(movement?.ScheduledTime),
            completed ? null : MapTime(revised ?? predicted),
            completed ? MapTime(revised) : null,
            EmptyToNull(movement?.Terminal),
            EmptyToNull(movement?.Gate));
    }

    private static FlightTimeResponse? MapTime(AeroDataBoxDateTime? value)
    {
        if (value is null ||
            (string.IsNullOrWhiteSpace(value.Local) && string.IsNullOrWhiteSpace(value.Utc)))
        {
            return null;
        }

        DateTimeOffset? utc = null;
        if (DateTimeOffset.TryParse(
                value.Utc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedUtc))
        {
            utc = parsedUtc;
        }

        return new FlightTimeResponse(EmptyToNull(value.Local), utc);
    }

    private static AirportResponse? MapAirport(
        AeroDataBoxAirport? value,
        AirportResponse? fallback)
    {
        var iata = EmptyToNull(value?.Iata) ?? fallback?.Iata;
        if (string.IsNullOrWhiteSpace(iata))
        {
            return null;
        }

        return new AirportResponse(
            iata.ToUpperInvariant(),
            EmptyToNull(value?.Icao)?.ToUpperInvariant() ?? fallback?.Icao,
            EmptyToNull(value?.Name) ?? EmptyToNull(value?.ShortName) ?? fallback?.Name ?? iata,
            EmptyToNull(value?.MunicipalityName) ?? fallback?.Municipality,
            EmptyToNull(value?.CountryCode)?.ToUpperInvariant() ?? fallback?.CountryCode,
            fallback?.Latitude,
            fallback?.Longitude,
            EmptyToNull(value?.TimeZone) ?? fallback?.TimeZone);
    }

    private static bool HasLiveQuality(AeroDataBoxMovement? movement) =>
        movement?.Quality.Any(item =>
            string.Equals(item, "Live", StringComparison.OrdinalIgnoreCase)) == true;

    private static DateTimeOffset? ParseUtcTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces |
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..20];
}
