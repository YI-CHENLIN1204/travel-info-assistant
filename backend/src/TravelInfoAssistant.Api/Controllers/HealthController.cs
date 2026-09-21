using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(ISystemHealthService healthService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<HealthResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<HealthResponse>>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ApiResponse<HealthResponse>>> Get(
        CancellationToken cancellationToken)
    {
        var health = await healthService.CheckAsync(cancellationToken);
        var response = ApiResponse<HealthResponse>.Success(
            health,
            "System",
            health.Status == "healthy" ? "realtime" : "unavailable");

        return health.Status == "healthy"
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
