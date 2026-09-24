using TravelInfoAssistant.Api.Contracts;

namespace TravelInfoAssistant.Api.Services.Emergency;

public interface IEmergencyInfoService
{
    Task<EmergencyInfoResponse?> GetAsync(Guid cityId, CancellationToken cancellationToken);
}
