using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Emergency;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/emergency")]
public sealed class EmergencyController(IEmergencyInfoService emergencyInfoService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<EmergencyInfoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EmergencyInfoResponse>>> Get(
        [FromQuery] Guid cityId,
        CancellationToken cancellationToken)
    {
        var data = await emergencyInfoService.GetAsync(cityId, cancellationToken);
        if (data is null)
        {
            return NotFound();
        }

        var sourceUpdatedAt = new DateTimeOffset(
            data.LastVerifiedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return Ok(new ApiResponse<EmergencyInfoResponse>(
            data,
            new ApiMeta(
                "scheduled",
                "官方來源人工確認資料",
                sourceUpdatedAt,
                DateTimeOffset.UtcNow,
                false,
                "電話僅供辨識與手動撥號；實際使用前請再次確認當地規則。")));
    }
}
