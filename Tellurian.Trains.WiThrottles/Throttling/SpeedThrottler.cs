using System.Diagnostics;

namespace Tellurian.Trains.WiThrottles.Throttling;

/// <summary>
/// Per-loco speed debouncing. Forwards a speed command when either the time threshold
/// or the step change threshold is exceeded. Ensures the final pending value is always
/// forwarded via a trailing edge timer.
/// </summary>
public sealed class SpeedThrottler(int timeThresholdMs, int stepThreshold, Func<byte, Task> forwardCallback) : IDisposable
{
    private readonly int _timeThresholdMs = timeThresholdMs;
    private readonly int _stepThreshold = stepThreshold;
    private readonly Func<byte, Task> _forwardCallback = forwardCallback;
    private readonly object _lock = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private byte _lastForwardedSpeed;
    private long _lastForwardedTimestamp;
    private byte? _pendingSpeed;
    private CancellationTokenSource? _trailingEdgeCts;
    private bool _disposed;

    /// <summary>
    /// Submits a speed value. Returns true if it was forwarded immediately, false if suppressed.
    /// Speed 0 is always forwarded immediately.
    /// </summary>
    public async Task<bool> SubmitAsync(byte speed)
    {
        if (_disposed) return false;

        // Speed 0 (stop) always forwards immediately
        if (speed == 0)
        {
            await ForwardNowAsync(speed);
            return true;
        }

        lock (_lock)
        {
            var elapsed = _stopwatch.ElapsedMilliseconds - _lastForwardedTimestamp;
            var stepChange = Math.Abs(speed - _lastForwardedSpeed);

            if (elapsed >= _timeThresholdMs || stepChange > _stepThreshold)
            {
                _pendingSpeed = null;
                CancelTrailingEdge();
                _lastForwardedSpeed = speed;
                _lastForwardedTimestamp = _stopwatch.ElapsedMilliseconds;
            }
            else
            {
                _pendingSpeed = speed;
                StartTrailingEdge(elapsed);
                return false;
            }
        }

        await _forwardCallback(speed);
        return true;
    }

    private async Task ForwardNowAsync(byte speed)
    {
        lock (_lock)
        {
            _pendingSpeed = null;
            CancelTrailingEdge();
            _lastForwardedSpeed = speed;
            _lastForwardedTimestamp = _stopwatch.ElapsedMilliseconds;
        }
        await _forwardCallback(speed);
    }

    private void StartTrailingEdge(long elapsed)
    {
        CancelTrailingEdge();
        var cts = new CancellationTokenSource();
        _trailingEdgeCts = cts;
        var delay = Math.Max(1, _timeThresholdMs - (int)elapsed);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cts.Token);
                byte? speedToForward;
                lock (_lock)
                {
                    speedToForward = _pendingSpeed;
                    if (speedToForward is null) return;
                    _pendingSpeed = null;
                    _lastForwardedSpeed = speedToForward.Value;
                    _lastForwardedTimestamp = _stopwatch.ElapsedMilliseconds;
                }
                await _forwardCallback(speedToForward.Value);
            }
            catch (OperationCanceledException) { }
        });
    }

    private void CancelTrailingEdge()
    {
        _trailingEdgeCts?.Cancel();
        _trailingEdgeCts?.Dispose();
        _trailingEdgeCts = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelTrailingEdge();
    }
}
