using System.Text.Json;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Providers.AeroDataBox;
using Xunit;

namespace TravelInfoAssistant.Api.Tests;

public sealed class AeroDataBoxFlightMapperTests
{
    private static readonly AirportResponse Origin = new(
        "TPE",
        "RCTP",
        "Taiwan Taoyuan International Airport",
        "Taipei",
        "TW",
        25.0777,
        121.233);

    [Fact]
    public void Deserialize_PreservesNonStandardProviderTimestamp()
    {
        const string json = """
            {
              "number": "BR891",
              "lastUpdatedUtc": "2026-09-23 23:40:00Z"
            }
            """;

        var flight = JsonSerializer.Deserialize<AeroDataBoxFlight>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(flight);
        Assert.Equal("2026-09-23 23:40:00Z", flight.LastUpdatedUtc);
    }

    [Fact]
    public void MapFidsFlight_MapsDirectLegAndEstimatedTimes()
    {
        var flight = new AeroDataBoxFlight
        {
            Number = "BR 198",
            Status = "Expected",
            Airline = new AeroDataBoxAirline { Name = "EVA Air", Iata = "BR" },
            Departure = Movement("TPE", "RCTP", "2026-09-23T08:50+08:00", "1", "A6"),
            Arrival = Movement(
                "NRT",
                "RJAA",
                "2026-09-23T13:15+09:00",
                "1",
                null,
                "2026-09-23T13:35+09:00"),
        };

        var result = AeroDataBoxFlightMapper.MapFidsFlight(flight, Origin);

        Assert.NotNull(result);
        Assert.Equal("BR 198", result.FlightNumber);
        Assert.Equal("TPE", result.Departure.Airport.Iata);
        Assert.Equal("NRT", result.Arrival.Airport.Iata);
        Assert.Equal("2026-09-23T13:35+09:00", result.Arrival.Estimated?.Local);
        Assert.Null(result.Arrival.Actual);
        Assert.Equal("A6", result.Departure.Gate);
    }

    [Fact]
    public void MapFlight_MapsRevisedTimesAsActualAfterArrival()
    {
        var flight = new AeroDataBoxFlight
        {
            Number = "BR198",
            Status = "Arrived",
            Departure = Movement(
                "TPE",
                "RCTP",
                "2026-09-23T08:50+08:00",
                "1",
                "A6",
                "2026-09-23T09:02+08:00"),
            Arrival = Movement(
                "NRT",
                "RJAA",
                "2026-09-23T13:15+09:00",
                "1",
                "42",
                "2026-09-23T13:28+09:00"),
            LastUpdatedUtc = "2026-09-23T04:40:00Z"
        };

        var result = AeroDataBoxFlightMapper.MapFlight(flight);

        Assert.NotNull(result);
        Assert.Equal("2026-09-23T09:02+08:00", result.Departure.Actual?.Local);
        Assert.Equal("2026-09-23T13:28+09:00", result.Arrival.Actual?.Local);
        Assert.Null(result.Arrival.Estimated);
        Assert.Equal("42", result.Arrival.Gate);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-23T04:40:00Z"),
            result.SourceUpdatedAt);
    }

    [Fact]
    public void MapFlight_AcceptsProviderTimestampWithSpaceSeparator()
    {
        var flight = new AeroDataBoxFlight
        {
            Number = "BR891",
            Status = "Expected",
            Departure = Movement("TPE", "RCTP", "2026-09-24T07:00+08:00", "2", "C6"),
            Arrival = Movement("HKG", "VHHH", "2026-09-24T08:55+08:00", "1", null),
            LastUpdatedUtc = "2026-09-23 23:40:00Z"
        };

        var result = AeroDataBoxFlightMapper.MapFlight(flight);

        Assert.NotNull(result);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-23T23:40:00Z"),
            result.SourceUpdatedAt);
    }

    [Fact]
    public void MapFlight_IgnoresInvalidProviderTimestamp()
    {
        var flight = new AeroDataBoxFlight
        {
            Number = "BR891",
            Status = "Expected",
            Departure = Movement("TPE", "RCTP", "2026-09-24T07:00+08:00", "2", "C6"),
            Arrival = Movement("HKG", "VHHH", "2026-09-24T08:55+08:00", "1", null),
            LastUpdatedUtc = "not-a-timestamp"
        };

        var result = AeroDataBoxFlightMapper.MapFlight(flight);

        Assert.NotNull(result);
        Assert.Null(result.SourceUpdatedAt);
    }

    [Fact]
    public void HasLiveData_UsesMovementQualityMarker()
    {
        var flight = new AeroDataBoxFlight
        {
            Departure = new AeroDataBoxMovement { Quality = ["Basic", "Live"] }
        };

        Assert.True(AeroDataBoxFlightMapper.HasLiveData(flight));
    }

    private static AeroDataBoxMovement Movement(
        string iata,
        string icao,
        string scheduled,
        string? terminal,
        string? gate,
        string? revised = null) =>
        new()
        {
            Airport = new AeroDataBoxAirport
            {
                Iata = iata,
                Icao = icao,
                Name = $"{iata} Airport"
            },
            ScheduledTime = Time(scheduled),
            RevisedTime = revised is null ? null : Time(revised),
            Terminal = terminal,
            Gate = gate,
            Quality = ["Basic"]
        };

    private static AeroDataBoxDateTime Time(string local) =>
        new() { Local = local };
}
