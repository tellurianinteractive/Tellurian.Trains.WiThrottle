using System.Text;
using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiFreds.Protocol;
using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Sessions;

/// <summary>
/// Protocol state machine: maps parsed <see cref="WiFredMessage"/> instances
/// to <see cref="ILoco"/> calls via the <see cref="ThrottledLocoController"/> and
/// produces response strings for the client.
/// </summary>
public sealed class SessionHandler
{
    private readonly ThrottleSession _session;
    private readonly ThrottledLocoController _controller;
    private readonly ActiveLocoTracker _tracker;
    private readonly string _sessionId;
    private readonly ILogger _logger;

    public SessionHandler(ThrottleSession session, ThrottledLocoController controller, ActiveLocoTracker tracker, string sessionId, ILogger logger)
    {
        _session = session;
        _controller = controller;
        _tracker = tracker;
        _sessionId = sessionId;
        _logger = logger;
    }

    public ThrottleSession Session => _session;

    /// <summary>
    /// Handles a parsed message and returns response lines to send to the client, or null if no response.
    /// </summary>
    public async Task<string?> HandleAsync(WiFredMessage message, CancellationToken cancellationToken = default)
    {
        _session.TouchActivity();

        return message switch
        {
            WiFredMessage.ThrottleName m => HandleThrottleName(m),
            WiFredMessage.HardwareId m => HandleHardwareId(m),
            WiFredMessage.HeartbeatOptIn => HandleHeartbeatOptIn(),
            WiFredMessage.Heartbeat => HandleHeartbeat(),
            WiFredMessage.Quit => await HandleQuitAsync(cancellationToken),
            WiFredMessage.AcquireLoco m => await HandleAcquireLocoAsync(m, cancellationToken),
            WiFredMessage.ReleaseLoco m => await HandleReleaseLocoAsync(m, cancellationToken),
            WiFredMessage.SetSpeed m => await HandleSetSpeedAsync(m, cancellationToken),
            WiFredMessage.SetDirection m => await HandleSetDirectionAsync(m, cancellationToken),
            WiFredMessage.EmergencyStop m => await HandleEmergencyStopAsync(m, cancellationToken),
            WiFredMessage.SetFunction m => await HandleSetFunctionAsync(m, cancellationToken),
            WiFredMessage.SetFunctionMode m => HandleSetFunctionMode(m),
            WiFredMessage.SetSpeedSteps => null,
            WiFredMessage.Unknown m => HandleUnknown(m),
            _ => null
        };
    }

    private string? HandleThrottleName(WiFredMessage.ThrottleName message)
    {
        _session.Name = message.Name;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Throttle name: {Name}", message.Name);
        return null;
    }

    private string? HandleHardwareId(WiFredMessage.HardwareId message)
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
        // If heartbeat was disabled after a timeout, re-enable monitoring
        // and re-register locos in the tracker (wiFRED recovered from WiFi loss).
        if (!_session.HeartbeatEnabled)
        {
            _session.HeartbeatEnabled = true;
            foreach (var loco in _session.Locos.Values)
                _tracker.MarkAcquired(loco.Address.Number, _sessionId);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Heartbeat recovered for {Name}, re-acquired {Count} locos",
                    _session.Name, _session.Locos.Count);
        }
        return null;
    }

    private async Task<string?> HandleAcquireLocoAsync(WiFredMessage.AcquireLoco message, CancellationToken cancellationToken)
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

        _tracker.MarkAcquired(address.Value.Number, _sessionId);

        var locoInfo = await _controller.GetLocoInfoAsync(address.Value, cancellationToken);
        if (locoInfo is not null)
        {
            loco.Speed = locoInfo.Speed.CurrentStep;
            loco.Direction = locoInfo.Direction;
            for (var i = 0; i < locoInfo.FunctionStates.Length && i < loco.FunctionStates.Length; i++)
                loco.FunctionStates[i] = locoInfo.FunctionStates[i];

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Acquired loco {LocoId} (address {Address}) with state from command station",
                    message.LocoId, address.Value.Number);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Acquired loco {LocoId} (address {Address}) with default state",
                    message.LocoId, address.Value.Number);
        }

        return BuildAcquisitionResponse(loco);
    }

    private async Task<string?> HandleReleaseLocoAsync(WiFredMessage.ReleaseLoco message, CancellationToken cancellationToken)
    {
        var loco = _session.GetLoco(message.LocoId);
        if (loco is null) return null;

        await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
        _controller.RemoveSpeedThrottler(loco.Address.Number);
        _tracker.MarkReleased(loco.Address.Number, _sessionId);
        _session.TryRemoveLoco(message.LocoId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Released loco {LocoId}", message.LocoId);
        return null;
    }

    private async Task<string?> HandleSetSpeedAsync(WiFredMessage.SetSpeed message, CancellationToken cancellationToken)
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

    private async Task<string?> HandleSetDirectionAsync(WiFredMessage.SetDirection message, CancellationToken cancellationToken)
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

    private async Task<string?> HandleEmergencyStopAsync(WiFredMessage.EmergencyStop message, CancellationToken cancellationToken)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            loco.Speed = 0;
            await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
        }
        return null;
    }

    private async Task<string?> HandleSetFunctionAsync(WiFredMessage.SetFunction message, CancellationToken cancellationToken)
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

    private string? HandleSetFunctionMode(WiFredMessage.SetFunctionMode message)
    {
        var locos = _session.GetTargetLocos(message.Target);
        foreach (var loco in locos)
        {
            if (message.FunctionNumber is >= 0 and <= 28)
                loco.FunctionMomentary[message.FunctionNumber] = message.Momentary;
        }
        return null;
    }

    private string? HandleUnknown(WiFredMessage.Unknown message)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Unknown message: {RawLine}", message.RawLine);
        return null;
    }

    /// <summary>
    /// Emergency stops all acquired locos without releasing them from the tracker.
    /// Called on heartbeat timeout where the session is still alive.
    /// </summary>
    public async Task EmergencyStopAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var loco in _session.Locos.Values)
        {
            loco.Speed = 0;
            await _controller.EmergencyStopAsync(loco.Address, cancellationToken);
            _logger.LogWarning("Emergency stopped loco {Address} in session {Name}",
                loco.Address.Number, _session.Name);
            _controller.RemoveSpeedThrottler(loco.Address.Number);
        }
    }

    /// <summary>
    /// Emergency stops all acquired locos and releases them from the tracker.
    /// Called on quit or disconnect when the session is ending.
    /// </summary>
    public async Task EmergencyStopAndReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        await EmergencyStopAllAsync(cancellationToken);
        _tracker.ReleaseAll(_sessionId);
    }

    private async Task<string?> HandleQuitAsync(CancellationToken cancellationToken)
    {
        await EmergencyStopAndReleaseAllAsync(cancellationToken);
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
