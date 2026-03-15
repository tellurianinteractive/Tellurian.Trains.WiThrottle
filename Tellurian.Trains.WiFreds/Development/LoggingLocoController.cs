using Tellurian.Trains.Communications.Interfaces.Locos;

namespace Tellurian.Trains.WiFreds.Development;

/// <summary>
/// Mock <see cref="ILoco"/> implementation that logs commands instead of sending to hardware.
/// Used in the Development environment.
/// </summary>
public sealed class LoggingLocoController(ILogger<LoggingLocoController> logger) : ILoco
{
    public async Task<bool> DriveAsync(Address address, Drive drive, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Drive loco {Address}: {Direction} speed {Speed}",
                address.Number, drive.Direction, drive.Speed.CurrentStep);
        return true;
    }

    public async Task<bool> EmergencyStopAsync(Address address, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Emergency stop loco {Address}", address.Number);
        return true;
    }

    public async Task<bool> SetFunctionAsync(Address address, Function locoFunction, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Function {Function} {State} on loco {Address}",
                locoFunction.Number, locoFunction.IsOn ? "ON" : "OFF", address.Number);
        return true;
    }
}
