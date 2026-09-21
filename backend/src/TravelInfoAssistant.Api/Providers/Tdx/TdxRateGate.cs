using Microsoft.Extensions.Options;
using TravelInfoAssistant.Api.Options;

namespace TravelInfoAssistant.Api.Providers.Tdx;

public interface ITdxRateGate
{
    bool TryAcquire();
}

public sealed class TdxRateGate(
    IOptions<TdxOptions> options,
    TimeProvider timeProvider) : ITdxRateGate
{
    private readonly Lock _lock = new();
    private readonly Queue<DateTimeOffset> _requests = new();

    public bool TryAcquire()
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddMinutes(-1);

        lock (_lock)
        {
            while (_requests.TryPeek(out var oldest) && oldest <= cutoff)
            {
                _requests.Dequeue();
            }

            var limit = Math.Max(1, options.Value.RequestsPerMinute);
            if (_requests.Count >= limit)
            {
                return false;
            }

            _requests.Enqueue(now);
            return true;
        }
    }
}
