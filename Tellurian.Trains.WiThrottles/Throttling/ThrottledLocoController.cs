using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiThrottles.Configuration;

namespace Tellurian.Trains.WiThrottles.Throttling;

/// <summary>
/// Wraps an <see cref="ILoco"/> with per-loco speed throttling and global rate limiting.
/// Emergency stops bypass all throttling.
/// </summary>
public sealed class ThrottledLocoController : IDisposable
{
    private readonly ILoco _inner;
    private readonly GlobalRateLimiter _rateLimiter;
    private readonly ThrottlingSettings _settings;
    private readonly ConcurrentDictionary<int, SpeedThrottler> _speedThrottlers = new();
    private readonly ILogger<ThrottledLocoController> _logger;

    public ThrottledLocoController(
        ILoco inner,
        IOptions<ThrottlingSettings> settings,
        ILogger<ThrottledLocoController> logger)
    {
        _inner = inner;
        _settings = settings.Value;
        _rateLimiter = new GlobalRateLimiter(_settings.GlobalMessageRatePerSecond);
        _logger = logger;
    }

    public async Task<bool> DriveAsync(Address address, Drive drive, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitForTokenAsync(cancellationToken);
        return await _inner.DriveAsync(address, drive, cancellationToken);
    }

    public async Task<bool> DriveWithSpeedThrottlingAsync(Address address, byte speed, Direction direction, CancellationToken cancellationToken = default)
    {
        var throttler = _speedThrottlers.GetOrAdd(address.Number, _ =>
            new SpeedThrottler(_settings.SpeedTimeThresholdMs, _settings.SpeedStepThreshold, async s =>
            {
                var drive = new Drive
                {
                    Direction = direction,
                    Speed = Speed.Set126(s)
                };
                await _rateLimiter.WaitForTokenAsync(cancellationToken);
                await _inner.DriveAsync(address, drive, cancellationToken);
            }));

        await throttler.SubmitAsync(speed);
        return true;
    }

    public async Task<bool> EmergencyStopAsync(Address address, CancellationToken cancellationToken = default)
    {
        // Emergency stops bypass rate limiting
        return await _inner.EmergencyStopAsync(address, cancellationToken);
    }

    public async Task<bool> SetFunctionAsync(Address address, Function locoFunction, CancellationToken cancellationToken = default)
    {
        await _rateLimiter.WaitForTokenAsync(cancellationToken);
        return await _inner.SetFunctionAsync(address, locoFunction, cancellationToken);
    }

    public void RemoveSpeedThrottler(int addressNumber)
    {
        if (_speedThrottlers.TryRemove(addressNumber, out var throttler))
            throttler.Dispose();
    }

    public void Dispose()
    {
        foreach (var throttler in _speedThrottlers.Values)
            throttler.Dispose();
        _speedThrottlers.Clear();
    }
}
