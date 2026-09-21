using System.Text.Json;
using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Infrastructure;

public sealed class ApiExceptionMiddleware(
    RequestDelegate next,
    ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API exception for {Path}.", context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse<object?>(
                null,
                new ApiMeta(
                    "unavailable",
                    "System",
                    null,
                    DateTimeOffset.UtcNow,
                    false,
                    "系統暫時無法處理此請求，請稍後再試。"));

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
