using System.Collections.Concurrent;

namespace Tellurian.Trains.WiFreds.Sessions;

/// <summary>
/// Tracks which loco addresses are currently acquired by active sessions.
/// Thread-safe singleton shared between TCP session handlers and Blazor UI.
/// </summary>
public sealed class ActiveLocoTracker(ILogger<ActiveLocoTracker> logger)
{
    // Maps loco address number → set of session IDs that have acquired it
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, byte>> _activeAddresses = new();
    private readonly ILogger _logger = logger;

    public void MarkAcquired(int addressNumber, string sessionId)
    {
        var sessions = _activeAddresses.GetOrAdd(addressNumber, _ => new ConcurrentDictionary<string, byte>());
        sessions[sessionId] = 0;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Address {Address} marked ACTIVE by session {SessionId}", addressNumber, sessionId);
    }

    public void MarkReleased(int addressNumber, string sessionId)
    {
        if (_activeAddresses.TryGetValue(addressNumber, out var sessions))
        {
            sessions.TryRemove(sessionId, out _);
            if (sessions.IsEmpty)
                _activeAddresses.TryRemove(addressNumber, out _);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Address {Address} marked RELEASED by session {SessionId}", addressNumber, sessionId);
        }
    }

    public void ReleaseAll(string sessionId)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Releasing ALL addresses for session {SessionId}", sessionId);
        foreach (var (address, sessions) in _activeAddresses)
        {
            sessions.TryRemove(sessionId, out _);
            if (sessions.IsEmpty)
                _activeAddresses.TryRemove(address, out _);
        }
    }

    public bool IsActive(int addressNumber) =>
        _activeAddresses.TryGetValue(addressNumber, out var sessions) && !sessions.IsEmpty;
}
