using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Flights;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/airports")]
public sealed class AirportsController(IAirportCatalog airportCatalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AirportResponse>>>(StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<IReadOnlyList<AirportResponse>>> Search(
        [FromQuery] string? q,
        [FromQuery] int limit = 10)
    {
        if (q?.Length > 80)
        {
            ModelState.AddModelError(nameof(q), "搜尋文字不可超過 80 個字元。");
        }

        if (limit is < 1 or > 20)
        {
            ModelState.AddModelError(nameof(limit), "回傳筆數必須介於 1 到 20。");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var airports = string.IsNullOrWhiteSpace(q)
            ? Array.Empty<AirportResponse>()
            : airportCatalog.Search(q, limit);
        return Ok(new ApiResponse<IReadOnlyList<AirportResponse>>(
            airports,
            new ApiMeta(
                "scheduled",
                "OurAirports",
                airportCatalog.GeneratedAt,
                DateTimeOffset.UtcNow,
                false,
                null)));
    }
}
