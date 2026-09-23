using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Flights;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/flights")]
public sealed partial class FlightsController(
    IFlightService flightService,
    IAirportCatalog airportCatalog) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FlightItineraryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FlightItineraryResponse>>>> Search(
        [FromQuery] string origin,
        [FromQuery] string destination,
        [FromQuery] string date,
        CancellationToken cancellationToken)
    {
        var originIata = ValidateAirport(origin, nameof(origin));
        var destinationIata = ValidateAirport(destination, nameof(destination));
        var parsedDate = ValidateDate(date);

        if (originIata is not null && destinationIata is not null &&
            string.Equals(originIata, destinationIata, StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(destination), "出發與抵達機場不可相同。");
        }

        if (!ModelState.IsValid || parsedDate is null)
        {
            return ValidationProblem(ModelState);
        }

        var result = await flightService.SearchRouteAsync(
            originIata!,
            destinationIata!,
            parsedDate.Value,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("provider/status")]
    [ProducesResponseType<ApiResponse<AeroDataBoxProviderStatusResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AeroDataBoxProviderStatusResponse>>> GetProviderStatus(
        CancellationToken cancellationToken)
    {
        var status = await flightService.GetProviderStatusAsync(cancellationToken);
        return Ok(ApiResponse<AeroDataBoxProviderStatusResponse>.Success(
            status,
            "Internal AeroDataBox usage meter",
            status.Configured ? "cached" : "unavailable",
            status.Configured ? null : "AeroDataBox 金鑰尚未設定。"));
    }

    [HttpGet("{flightNumber}")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<FlightItineraryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FlightItineraryResponse>>>> GetFlight(
        string flightNumber,
        [FromQuery] string date,
        CancellationToken cancellationToken)
    {
        var normalizedNumber = new string((flightNumber ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (!FlightNumberPattern().IsMatch(normalizedNumber))
        {
            ModelState.AddModelError(nameof(flightNumber), "請提供 2 到 8 碼的有效航班編號。");
        }

        var parsedDate = ValidateDate(date);
        if (!ModelState.IsValid || parsedDate is null)
        {
            return ValidationProblem(ModelState);
        }

        var result = await flightService.GetFlightAsync(
            normalizedNumber,
            parsedDate.Value,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    private string? ValidateAirport(string? value, string field)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!IataPattern().IsMatch(normalized) || airportCatalog.FindByIata(normalized) is null)
        {
            ModelState.AddModelError(field, "請從機場搜尋結果選擇有效的 IATA 機場代碼。");
            return null;
        }

        return normalized;
    }

    private DateOnly? ValidateDate(string? value)
    {
        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            ModelState.AddModelError("date", "日期格式必須為 YYYY-MM-DD。");
            return null;
        }

        return date;
    }

    private static ApiResponse<T> ToResponse<T>(ProviderQueryResult<T> result) =>
        new(
            result.Data,
            new ApiMeta(
                result.DataStatus,
                "AeroDataBox",
                result.SourceUpdatedAt,
                result.FetchedAt,
                result.Stale,
                result.Message));

    [GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex IataPattern();

    [GeneratedRegex("^[A-Z0-9]{2,8}$", RegexOptions.CultureInvariant)]
    private static partial Regex FlightNumberPattern();
}
