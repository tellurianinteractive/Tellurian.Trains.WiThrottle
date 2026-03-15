using Tellurian.Trains.Communications.Interfaces.Locos;

namespace Tellurian.Trains.WiFreds.Sessions;

/// <summary>
/// Mutable per-loco state tracked by the server.
/// </summary>
public sealed class LocoState
{
    public LocoState(Address address, string locoId)
    {
        Address = address;
        LocoId = locoId;
    }

    public Address Address { get; }
    public string LocoId { get; }
    public byte Speed { get; set; }
    public Direction Direction { get; set; } = Direction.Forward;
    public bool[] FunctionStates { get; } = new bool[29];
    public bool[] FunctionMomentary { get; } = new bool[29];

    public Drive CurrentDrive => new()
    {
        Direction = Direction,
        Speed = Communications.Interfaces.Locos.Speed.Set126(Speed)
    };
}
