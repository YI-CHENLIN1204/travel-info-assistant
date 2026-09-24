using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.MetNorway;

public interface IMetNorwayRateGate
{
    Task WaitAsync(CancellationToken cancellationToken);
}

public sealed class MetNorwayRateGate(
    IOptions<MetNorwayOptions> options,
    TimeProvider timeProvider) : IMetNorwayRateGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextAllowedAt = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            var wait = _nextAllowedAt - now;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, timeProvider, cancellationToken);
            }

            var requestsPerSecond = Math.Clamp(options.Value.RequestsPerSecond, 1, 20);
            _nextAllowedAt = timeProvider.GetUtcNow().AddSeconds(1d / requestsPerSecond);
        }
        finally
        {
            _gate.Release();
        }
    }
}
