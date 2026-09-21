using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/cities")]
public sealed class CitiesController(ICityService cityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CitySummaryResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CitySummaryResponse>>>> GetAll(
        CancellationToken cancellationToken)
    {
        var cities = await cityService.GetCitiesAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<CitySummaryResponse>>.Success(cities, "City master data"));
    }

    [HttpGet("{cityId:guid}/services")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<ServiceCapabilityResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ServiceCapabilityResponse>>>> GetServices(
        Guid cityId,
        CancellationToken cancellationToken)
    {
        var city = await cityService.GetCityAsync(cityId, cancellationToken);
        if (city is null)
        {
            return NotFound();
        }

        return Ok(ApiResponse<IReadOnlyList<ServiceCapabilityResponse>>.Success(
            city.Services,
            "City master data"));
    }
}
