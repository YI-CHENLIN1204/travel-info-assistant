using Microsoft.AspNetCore.Mvc;
using TravelInfoAssistant.Api.Contracts;
using TravelInfoAssistant.Api.Services.Transit;
using TravelInfoAssistant.Api.Services.Weather;

namespace TravelInfoAssistant.Api.Controllers;

[ApiController]
[Route("api/v1/weather")]
public sealed class WeatherController(IWeatherService weatherService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<WeatherResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WeatherResponse?>>> GetCityWeather(
        [FromQuery] Guid cityId,
        CancellationToken cancellationToken)
    {
        var result = await weatherService.GetCityWeatherAsync(cityId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(result));
    }

    [HttpGet("location")]
    [ProducesResponseType<ApiResponse<WeatherResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<WeatherResponse?>>> GetLocationWeather(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken cancellationToken)
    {
        if (lat is < -90 or > 90)
        {
            ModelState.AddModelError(nameof(lat), "緯度必須介於 -90 到 90。 ");
        }

        if (lon is < -180 or > 180)
        {
            ModelState.AddModelError(nameof(lon), "經度必須介於 -180 到 180。 ");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var result = await weatherService.GetLocationWeatherAsync(
            lat,
            lon,
            cancellationToken);
        return Ok(ToResponse(result));
    }

    private static ApiResponse<WeatherResponse?> ToResponse(
        ProviderQueryResult<WeatherResponse?> result) =>
        new(
            result.Data,
            new ApiMeta(
                result.DataStatus,
                "MET Norway",
                result.SourceUpdatedAt,
                result.FetchedAt,
                result.Stale,
                result.Message));
}
