using Tellurian.Trains.Communications.Interfaces.Locos;

namespace Tellurian.Trains.WiThrottles.Tests.Helpers;

public sealed class RecordingLocoController : ILoco
{
    public List<LocoCall> Calls { get; } = [];

    public Task<bool> DriveAsync(Address address, Drive drive, CancellationToken cancellationToken = default)
    {
        Calls.Add(new LocoCall("Drive", address, drive, null));
        return Task.FromResult(true);
    }

    public Task<bool> EmergencyStopAsync(Address address, CancellationToken cancellationToken = default)
    {
        Calls.Add(new LocoCall("EmergencyStop", address, null, null));
        return Task.FromResult(true);
    }

    public Task<bool> SetFunctionAsync(Address address, Function locoFunction, CancellationToken cancellationToken = default)
    {
        Calls.Add(new LocoCall("SetFunction", address, null, locoFunction));
        return Task.FromResult(true);
    }

    public IEnumerable<LocoCall> DriveCalls => Calls.Where(c => c.Method == "Drive");
    public IEnumerable<LocoCall> EmergencyStopCalls => Calls.Where(c => c.Method == "EmergencyStop");
    public IEnumerable<LocoCall> SetFunctionCalls => Calls.Where(c => c.Method == "SetFunction");
}

public sealed record LocoCall(string Method, Address Address, Drive? Drive, Function? Function);
