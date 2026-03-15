namespace Tellurian.Trains.WiFreds.Throttling;

/// <summary>
/// Token bucket rate limiter for global message rate limiting.
/// Emergency stops are exempt and always pass through.
/// </summary>
public sealed class GlobalRateLimiter(int messagesPerSecond)
{
    private readonly int _maxTokens = messagesPerSecond;
    private readonly double _refillRatePerMs = messagesPerSecond / 1000.0;
    private readonly object _lock = new();
    private double _tokens = messagesPerSecond;
    private long _lastRefillTimestamp = Environment.TickCount64;

    /// <summary>
    /// Attempts to acquire a token. Returns true if the message can be sent immediately.
    /// If false, the caller should delay and retry.
    /// </summary>
    public bool TryAcquire()
    {
        lock (_lock)
        {
            Refill();
            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Waits until a token is available, then acquires it.
    /// </summary>
    public async Task WaitForTokenAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (TryAcquire()) return;
            await Task.Delay(5, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void Refill()
    {
        var now = Environment.TickCount64;
        var elapsed = now - _lastRefillTimestamp;
        if (elapsed <= 0) return;

        _tokens = Math.Min(_maxTokens, _tokens + elapsed * _refillRatePerMs);
        _lastRefillTimestamp = now;
    }
}
