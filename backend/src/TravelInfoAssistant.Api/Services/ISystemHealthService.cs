using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services;

public interface ISystemHealthService
{
    Task<HealthResponse> CheckAsync(CancellationToken cancellationToken);
}
