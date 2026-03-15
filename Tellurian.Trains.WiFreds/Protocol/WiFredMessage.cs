namespace Tellurian.Trains.WiFreds.Protocol;

/// <summary>
/// Discriminated union of all parsed WiFred protocol messages.
/// </summary>
public abstract record WiFredMessage
{
    private WiFredMessage() { }

    /// <summary>Client sends its human-readable name. Format: N{name}</summary>
    public sealed record ThrottleName(string Name) : WiFredMessage;

    /// <summary>Client sends hardware unique ID. Format: HU{macHex}</summary>
    public sealed record HardwareId(string Id) : WiFredMessage;

    /// <summary>Client opts in to heartbeat monitoring. Format: *+</summary>
    public sealed record HeartbeatOptIn : WiFredMessage;

    /// <summary>Client sends heartbeat keepalive. Format: *</summary>
    public sealed record Heartbeat : WiFredMessage;

    /// <summary>Client disconnects. Format: Q</summary>
    public sealed record Quit : WiFredMessage;

    /// <summary>Client acquires a loco. Format: MT+{locoId}&lt;;&gt;{locoId}</summary>
    public sealed record AcquireLoco(string LocoId) : WiFredMessage;

    /// <summary>Client releases a loco. Format: MT-{locoId}&lt;;&gt;r</summary>
    public sealed record ReleaseLoco(string LocoId) : WiFredMessage;

    /// <summary>Client sets speed. Format: MTA{target}&lt;;&gt;V{speed}</summary>
    public sealed record SetSpeed(string Target, byte Speed) : WiFredMessage;

    /// <summary>Client sets direction. Format: MTA{target}&lt;;&gt;R{0or1}</summary>
    public sealed record SetDirection(string Target, bool Forward) : WiFredMessage;

    /// <summary>Client requests emergency stop. Format: MTA{target}&lt;;&gt;X</summary>
    public sealed record EmergencyStop(string Target) : WiFredMessage;

    /// <summary>Client sets function state (toggle F or force f). Format: MTA{target}&lt;;&gt;F{0or1}{num} or f{0or1}{num}</summary>
    public sealed record SetFunction(string Target, int FunctionNumber, bool On, bool IsForce) : WiFredMessage;

    /// <summary>Client sets function momentary/locking mode. Format: MTA{target}&lt;;&gt;m{0or1}{num}</summary>
    public sealed record SetFunctionMode(string Target, int FunctionNumber, bool Momentary) : WiFredMessage;

    /// <summary>Client sets speed step mode. Format: MTA{target}&lt;;&gt;s{mode}</summary>
    public sealed record SetSpeedSteps(string Target, int Steps) : WiFredMessage;

    /// <summary>Unrecognized message.</summary>
    public sealed record Unknown(string RawLine) : WiFredMessage;
}
