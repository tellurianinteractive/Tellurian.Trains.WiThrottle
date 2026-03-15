namespace Tellurian.Trains.WiFreds.Sessions;

/// <summary>
/// Per-client session state for a connected WiFred throttle.
/// </summary>
public sealed class ThrottleSession
{
    private readonly Dictionary<string, LocoState> _locos = new(StringComparer.Ordinal);

    public string? Name { get; set; }
    public string? HardwareId { get; set; }
    public bool HeartbeatEnabled { get; set; }
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;

    public IReadOnlyDictionary<string, LocoState> Locos => _locos;

    public bool TryAddLoco(LocoState loco)
    {
        if (_locos.Count >= 4) return false;
        return _locos.TryAdd(loco.LocoId, loco);
    }

    public bool TryRemoveLoco(string locoId) => _locos.Remove(locoId);

    public LocoState? GetLoco(string locoId) =>
        _locos.TryGetValue(locoId, out var loco) ? loco : null;

    public IEnumerable<LocoState> GetTargetLocos(string target) =>
        target == "*" ? _locos.Values : _locos.TryGetValue(target, out var loco) ? [loco] : [];

    public void TouchActivity() => LastActivity = DateTimeOffset.UtcNow;
}
