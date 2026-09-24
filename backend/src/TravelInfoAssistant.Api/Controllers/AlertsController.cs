using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Alerts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
public sealed class AlertsController(ITravelAlertService alertService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TravelAlertResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TravelAlertResponse>>>> GetCityAlerts(
        [FromQuery] Guid cityId,
        CancellationToken cancellationToken)
    {
        var result = await alertService.GetCityAlertsAsync(cityId, cancellationToken);
        return result is null ? NotFound() : Ok(ToResponse(result));
    }

    [HttpGet("popular")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TravelAlertResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TravelAlertResponse>>>> GetPopularAlerts(
        CancellationToken cancellationToken)
    {
        var result = await alertService.GetPopularAlertsAsync(cancellationToken);
        return Ok(ToResponse(result));
    }

    private static ApiResponse<IReadOnlyList<TravelAlertResponse>> ToResponse(
        ProviderQueryResult<IReadOnlyList<TravelAlertResponse>> result) =>
        new(
            result.Data,
            new ApiMeta(
                result.DataStatus,
                "外交部領事事務局 (BOCA)",
                result.SourceUpdatedAt,
                result.FetchedAt,
                result.Stale,
                result.Message));
}
