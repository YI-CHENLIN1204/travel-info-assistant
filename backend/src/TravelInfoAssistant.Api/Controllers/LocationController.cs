using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/location")]
public sealed class LocationController(ICityService cityService) : ControllerBase
{
    [HttpPost("resolve-city")]
    [ProducesResponseType<ApiResponse<ResolveCityResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ResolveCityResponse>>> ResolveCity(
        [FromBody] ResolveCityRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return BadRequest();
        }

        var result = await cityService.ResolveCityAsync(
            request.Latitude,
            request.Longitude,
            cancellationToken);

        return Ok(ApiResponse<ResolveCityResponse>.Success(
            result,
            "Supported city resolver",
            "scheduled",
            result.Supported ? null : "目前位置尚未有已整合的城市服務。"));
    }
}
