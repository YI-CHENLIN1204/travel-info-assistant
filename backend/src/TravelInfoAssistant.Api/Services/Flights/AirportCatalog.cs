using System.Globalization;
using System.Text;
using System.Text.Json;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services.Flights;

public sealed class AirportCatalog : IAirportCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IReadOnlyList<CatalogAirport> _airports;
    private readonly IReadOnlyDictionary<string, CatalogAirport> _byIata;

    public AirportCatalog(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "airports.min.json");
        using var stream = File.OpenRead(path);
        var snapshot = JsonSerializer.Deserialize<AirportCatalogSnapshot>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The local airport catalogue could not be loaded.");

        GeneratedAt = snapshot.GeneratedAt;
        _airports = snapshot.Airports
            .Select(item => item with { SearchText = BuildSearchText(item) })
            .ToList();
        _byIata = _airports.ToDictionary(item => item.Iata, StringComparer.OrdinalIgnoreCase);
    }

    public DateTimeOffset GeneratedAt { get; }

    public IReadOnlyList<AirportResponse> Search(string query, int limit)
    {
        var normalized = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return _airports
            .Select(item => new { Airport = item, Rank = GetRank(item, normalized) })
            .Where(item => item.Rank < int.MaxValue)
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.Airport.Municipality ?? item.Airport.Name)
            .ThenBy(item => item.Airport.Iata)
            .Take(limit)
            .Select(item => Map(item.Airport))
            .ToList();
    }

    public AirportResponse? FindByIata(string iata) =>
        _byIata.TryGetValue(iata.Trim(), out var airport) ? Map(airport) : null;

    private static int GetRank(CatalogAirport airport, string query)
    {
        if (Normalize(airport.Iata) == query)
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(airport.Icao) && Normalize(airport.Icao) == query)
        {
            return 1;
        }

        if (Normalize(airport.Municipality).StartsWith(query, StringComparison.Ordinal) ||
            Normalize(airport.Name).StartsWith(query, StringComparison.Ordinal))
        {
            return 2;
        }

        return airport.SearchText.Contains(query, StringComparison.Ordinal)
            ? 3
            : int.MaxValue;
    }

    private static string BuildSearchText(CatalogAirport airport) => Normalize(string.Join(
        " ",
        airport.Iata,
        airport.Icao,
        airport.Name,
        airport.Municipality,
        airport.CountryCode,
        airport.Keywords));

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static AirportResponse Map(CatalogAirport item) => new(
        item.Iata,
        item.Icao,
        item.Name,
        item.Municipality,
        item.CountryCode,
        item.Latitude,
        item.Longitude);

    private sealed record AirportCatalogSnapshot(
        DateTimeOffset GeneratedAt,
        IReadOnlyList<CatalogAirport> Airports);

    private sealed record CatalogAirport(
        string Iata,
        string? Icao,
        string Name,
        string? Municipality,
        string? CountryCode,
        double Latitude,
        double Longitude,
        string? Keywords)
    {
        public string SearchText { get; init; } = string.Empty;
    }
}
