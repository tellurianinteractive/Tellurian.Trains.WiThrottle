namespace Tellurian.Trains.WiThrottles.Configuration;

public sealed record WiThrottleSettings
{
    public int Port { get; init; } = 12090;
    public int HeartbeatTimeoutSeconds { get; init; } = 10;
    public string ServiceName { get; init; } = "WiThrottle Server";
}
