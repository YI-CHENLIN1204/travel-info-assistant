using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/transit")]
public sealed class TransitController(ITransitService transitService) : ControllerBase
{
    [HttpGet("modes")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitModeResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitModeResponse>>>> GetModes(
        [FromQuery] Guid cityId,
        CancellationToken cancellationToken)
    {
        var modes = await transitService.GetModesAsync(cityId, cancellationToken);
        return modes is null
            ? NotFound()
            : Ok(ApiResponse<IReadOnlyList<TransitModeResponse>>.Success(
                modes,
                "City capability matrix"));
    }

    [HttpGet("bus/routes")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitRouteResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitRouteResponse>>>> SearchBusRoutes(
        [FromQuery] Guid cityId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (q?.Length > 50)
        {
            ModelState.AddModelError(nameof(q), "搜尋文字不可超過 50 個字元。");
            return ValidationProblem(ModelState);
        }

        var result = await transitService.SearchBusRoutesAsync(cityId, q, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("bus/stops")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitStopResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitStopResponse>>>> GetBusStops(
        [FromQuery] Guid cityId,
        [FromQuery] string routeName,
        [FromQuery] int direction = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(routeName) || routeName.Length > 50)
        {
            ModelState.AddModelError(nameof(routeName), "請提供有效的公車路線名稱。");
        }

        if (direction is not (0 or 1))
        {
            ModelState.AddModelError(nameof(direction), "方向只能是 0 或 1。");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await transitService.GetBusStopsAsync(
            cityId,
            routeName,
            direction,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("bus/arrivals")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>> GetBusArrivals(
        [FromQuery] Guid cityId,
        [FromQuery] string routeName,
        [FromQuery] int direction,
        [FromQuery] string stopId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(routeName) || routeName.Length > 50)
        {
            ModelState.AddModelError(nameof(routeName), "請提供有效的公車路線名稱。");
        }

        if (string.IsNullOrWhiteSpace(stopId) || stopId.Length > 80)
        {
            ModelState.AddModelError(nameof(stopId), "請提供有效的站牌代碼。");
        }

        if (direction is not (0 or 1))
        {
            ModelState.AddModelError(nameof(direction), "方向只能是 0 或 1。");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await transitService.GetBusArrivalsAsync(
            cityId,
            routeName,
            direction,
            stopId,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("metro/stations")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MetroStationResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MetroStationResponse>>>> SearchMetroStations(
        [FromQuery] Guid cityId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (q?.Length > 50)
        {
            ModelState.AddModelError(nameof(q), "搜尋文字不可超過 50 個字元。");
            return ValidationProblem(ModelState);
        }

        var result = await transitService.SearchMetroStationsAsync(cityId, q, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("metro/arrivals")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>> GetMetroArrivals(
        [FromQuery] Guid cityId,
        [FromQuery] string stationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationId) || stationId.Length > 80)
        {
            ModelState.AddModelError(nameof(stationId), "請提供有效的捷運站代碼。");
            return ValidationProblem(ModelState);
        }

        var result = await transitService.GetMetroArrivalsAsync(
            cityId,
            stationId,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("rail/stations")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<RailStationResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RailStationResponse>>>> SearchRailStations(
        [FromQuery] Guid cityId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        if (q?.Length > 50)
        {
            ModelState.AddModelError(nameof(q), "搜尋文字不可超過 50 個字元。");
            return ValidationProblem(ModelState);
        }

        var result = await transitService.SearchRailStationsAsync(cityId, q, cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("rail/arrivals")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TransitArrivalResponse>>>> GetRailArrivals(
        [FromQuery] Guid cityId,
        [FromQuery] string stationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stationId) || stationId.Length > 80)
        {
            ModelState.AddModelError(nameof(stationId), "請提供有效的台鐵車站代碼。");
            return ValidationProblem(ModelState);
        }

        var result = await transitService.GetRailArrivalsAsync(
            cityId,
            stationId,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    [HttpGet("tdx/status")]
    [ProducesResponseType<ApiResponse<TdxProviderStatusResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TdxProviderStatusResponse>>> GetTdxStatus(
        CancellationToken cancellationToken)
    {
        var status = await transitService.GetTdxStatusAsync(cancellationToken);
        return Ok(ApiResponse<TdxProviderStatusResponse>.Success(
            status,
            "Internal TDX usage meter",
            status.Configured ? "cached" : "unavailable",
            status.Configured ? null : "TDX 金鑰尚未設定。"));
    }

    private static ApiResponse<T> ToResponse<T>(ProviderQueryResult<T> result) =>
        new(
            result.Data,
            new ApiMeta(
                result.DataStatus,
                "TDX",
                result.SourceUpdatedAt,
                result.FetchedAt,
                result.Stale,
                result.Message));
}
