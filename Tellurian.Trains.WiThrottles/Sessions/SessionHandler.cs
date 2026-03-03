using System.Text;
using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiThrottles.Protocol;
using Tellurian.Trains.WiThrottles.Throttling;

namespace Tellurian.Trains.WiThrottles.Sessions;

/// <summary>
/// Protocol state machine: maps parsed <see cref="WiThrottleMessage"/> instances
/// to <see cref="ILoco"/> calls via the <see cref="ThrottledLocoController"/> and
/// produces response strings for the client.
/// </summary>
public sealed class SessionHandler
{
    private readonly ThrottleSession _session;
    private readonly ThrottledLocoController _controller;
    private readonly ILogger _logger;

    public SessionHandler(ThrottleSession session, ThrottledLocoController controller, ILogger logger)
    {
        _session = session;
        _controller = controller;
        _logger = logger;
    }

    public ThrottleSession Session => _session;

    /// <summary>
    /// Handles a parsed message and returns response lines to send to the client, or null if no response.
    /// </summary>
    public async Task<string?> HandleAsync(WiThrottleMessage message, CancellationToken cancellationToken = default)
    {
        _session.TouchActivity();

        return message switch
        {
            WiThrottleMessage.ThrottleName m => HandleThrottleName(m),
            WiThrottleMessage.HardwareId m => HandleHardwareId(m),
            WiThrottleMessage.HeartbeatOptIn => HandleHeartbeatOptIn(),
            WiThrottleMessage.Heartbeat => HandleHeartbeat(),
            WiThrottleMessage.Quit => await HandleQuitAsync(cancellationToken),
            WiThrottleMessage.AcquireLoco m => HandleAcquireLoco(m),
            WiThrottleMessage.ReleaseLoco m => await HandleReleaseLocoAsync(m, cancellationToken),
            WiThrottleMessage.SetSpeed m => await HandleSetSpeedAsync(m, cancellationToken),
            WiThrottleMessage.SetDirection m => await HandleSetDirectionAsync(m, cancellationToken),
            WiThrottleMessage.EmergencyStop m => await HandleEmergencyStopAsync(m, cancellationToken),
            WiThrottleMessage.SetFunction m => await HandleSetFunctionAsync(m, cancellationToken),
            WiThrottleMessage.SetFunctionMode m => HandleSetFunctionMode(m),
            WiThrottleMessage.SetSpeedSteps => null,
            WiThrottleMessage.Unknown m => HandleUnknown(m),
            _ => null
        };
    }

    private string? HandleThrottleName(WiThrottleMessage.ThrottleName message)
    {
        _session.Name = message.Name;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Throttle name: {Name}", message.Name);
        return null;
    }

    private string? HandleHardwareId(WiThrottleMessage.HardwareId message)
    {
        _session.HardwareId = message.Id;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Hardware ID: {Id}", message.Id);
        return null;
    }

    private string? HandleHeartbeatOptIn()
    {
        _session.HeartbeatEnabled = true;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Heartbeat enabled for {Name}", _session.Name);
        return null;
    }

    private string? HandleHeartbeat()
    {
        // Activity already touched above
        return null;
    }

    private string? HandleAcquireLoco(WiThrottleMessage.AcquireLoco message)
    {
        var address = LocoAddress.TryParse(message.LocoId);
        if (address is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Invalid loco address: {LocoId}", message.LocoId);
            return null;
        }

        var loco = new LocoState(address.Value, message.LocoId);
        if (!_session.TryAddLoco(loco))
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Cannot acquire loco {LocoId}: maximum 4 locos reached", message.LocoId);
            return null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Acquired loco {LocoId} (address {Address})", message.LocoId, address.Value.Number);

        return BuildAcquisitionResponse(loco);
    }

    private async Task<string?> HandleReleaseLocoAsync(WiThrottleMessage.ReleaseLoco message, CancellationToken cancellationToken)
    {
        var loco = _session.GetLoco(message.LocoId);
        if (loco is null) return null;

        await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
        _controller.RemoveSpeedThrottler(loco.Address.Number);
        _session.TryRemoveLoco(message.LocoId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Released loco {LocoId}", message.LocoId);
        return null;
    }

    private async Task<string?> HandleSetSpeedAsync(WiThrottleMessage.SetSpeed message, CancellationToken cancellationToken)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            loco.Speed = message.Speed;
            await _controller.DriveWithSpeedThrottlingAsync(
                loco.Address, message.Speed, loco.Direction, cancellationToken);
        }
        return null;
    }

    private async Task<string?> HandleSetDirectionAsync(WiThrottleMessage.SetDirection message, CancellationToken cancellationToken)
    {
        var direction = message.Forward ? Direction.Forward : Direction.Backward;
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            loco.Direction = direction;
            // Direction changes require resending current speed via DriveAsync (not speed-throttled)
            await _controller.DriveAsync(loco.Address, loco.CurrentDrive, cancellationToken);
        }
        return null;
    }

    private async Task<string?> HandleEmergencyStopAsync(WiThrottleMessage.EmergencyStop message, CancellationToken cancellationToken)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            loco.Speed = 0;
            await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
        }
        return null;
    }

    private async Task<string?> HandleSetFunctionAsync(WiThrottleMessage.SetFunction message, CancellationToken cancellationToken)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            if (message.FunctionNumber is >= 0 and <= 28)
            {
                bool newState;
                if (message.IsForce)
                {
                    // Force (f command): directly set the function state
                    newState = message.On;
                }
                else if (loco.FunctionMomentary[message.FunctionNumber])
                {
                    // Momentary function: pass button state directly
                    newState = message.On;
                }
                else
                {
                    // Latching function: toggle on button press, ignore release
                    if (!message.On) continue;
                    newState = !loco.FunctionStates[message.FunctionNumber];
                }

                loco.FunctionStates[message.FunctionNumber] = newState;
                var funcEnum = (Functions)message.FunctionNumber;
                var function = Function.Set(funcEnum, newState);
                await _controller.SetFunctionAsync(loco.Address, function, cancellationToken);
            }
        }
        return null;
    }

    private string? HandleSetFunctionMode(WiThrottleMessage.SetFunctionMode message)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            if (message.FunctionNumber is >= 0 and <= 28)
                loco.FunctionMomentary[message.FunctionNumber] = message.Momentary;
        }
        return null;
    }

    private string? HandleUnknown(WiThrottleMessage.Unknown message)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Unknown message: {RawLine}", message.RawLine);
        return null;
    }

    /// <summary>
    /// Emergency stops all acquired locos and releases them. Called on quit or disconnect.
    /// </summary>
    public async Task EmergencyStopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var loco in _session.Locos.Values)
        {
            loco.Speed = 0;
            await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
            _controller.RemoveSpeedThrottler(loco.Address.Number);
        }
    }

    private async Task<string?> HandleQuitAsync(CancellationToken cancellationToken)
    {
        await EmergencyStopAllAsync(cancellationToken);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Client {Name} quit", _session.Name);
        return null;
    }

    /// <summary>
    /// Builds the multi-line acquisition response: function states, direction, speed step mode.
    /// </summary>
    private static string BuildAcquisitionResponse(LocoState loco)
    {
        var sb = new StringBuilder();

        // Send function states F0-F28
        for (var i = 0; i <= 28; i++)
        {
            var state = loco.FunctionStates[i] ? '1' : '0';
            sb.Append($"MTA{loco.LocoId}<;>F{state}{i}\n");
        }

        // Send current direction (R1 = forward, R0 = reverse)
        var dir = loco.Direction == Direction.Forward ? '1' : '0';
        sb.Append($"MTA{loco.LocoId}<;>R{dir}\n");

        // Send speed step mode as end marker
        sb.Append($"MTA{loco.LocoId}<;>s128\n");

        return sb.ToString();
    }
}
