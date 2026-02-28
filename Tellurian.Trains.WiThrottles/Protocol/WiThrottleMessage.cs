namespace Tellurian.Trains.WiThrottles.Protocol;

/// <summary>
/// Discriminated union of all parsed WiThrottle protocol messages.
/// </summary>
public abstract record WiThrottleMessage
{
    private WiThrottleMessage() { }

    /// <summary>Client sends its human-readable name. Format: N{name}</summary>
    public sealed record ThrottleName(string Name) : WiThrottleMessage;

    /// <summary>Client sends hardware unique ID. Format: HU{macHex}</summary>
    public sealed record HardwareId(string Id) : WiThrottleMessage;

    /// <summary>Client opts in to heartbeat monitoring. Format: *+</summary>
    public sealed record HeartbeatOptIn : WiThrottleMessage;

    /// <summary>Client sends heartbeat keepalive. Format: *</summary>
    public sealed record Heartbeat : WiThrottleMessage;

    /// <summary>Client disconnects. Format: Q</summary>
    public sealed record Quit : WiThrottleMessage;

    /// <summary>Client acquires a loco. Format: MT+{locoId}&lt;;&gt;{locoId}</summary>
    public sealed record AcquireLoco(string LocoId) : WiThrottleMessage;

    /// <summary>Client releases a loco. Format: MT-{locoId}&lt;;&gt;r</summary>
    public sealed record ReleaseLoco(string LocoId) : WiThrottleMessage;

    /// <summary>Client sets speed. Format: MTA{target}&lt;;&gt;V{speed}</summary>
    public sealed record SetSpeed(string Target, byte Speed) : WiThrottleMessage;

    /// <summary>Client sets direction. Format: MTA{target}&lt;;&gt;R{0or1}</summary>
    public sealed record SetDirection(string Target, bool Forward) : WiThrottleMessage;

    /// <summary>Client requests emergency stop. Format: MTA{target}&lt;;&gt;X</summary>
    public sealed record EmergencyStop(string Target) : WiThrottleMessage;

    /// <summary>Client sets function state (toggle F or force f). Format: MTA{target}&lt;;&gt;F{0or1}{num} or f{0or1}{num}</summary>
    public sealed record SetFunction(string Target, int FunctionNumber, bool On) : WiThrottleMessage;

    /// <summary>Client sets function momentary/locking mode. Format: MTA{target}&lt;;&gt;m{0or1}{num}</summary>
    public sealed record SetFunctionMode(string Target, int FunctionNumber, bool Momentary) : WiThrottleMessage;

    /// <summary>Client sets speed step mode. Format: MTA{target}&lt;;&gt;s{mode}</summary>
    public sealed record SetSpeedSteps(string Target, int Steps) : WiThrottleMessage;

    /// <summary>Unrecognized message.</summary>
    public sealed record Unknown(string RawLine) : WiThrottleMessage;
}
