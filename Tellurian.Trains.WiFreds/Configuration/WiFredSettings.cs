namespace Tellurian.Trains.WiFreds.Configuration;

public sealed record WiFredSettings
{
    public int Port { get; init; } = 12090;
    public int HeartbeatTimeoutSeconds { get; init; } = 10;
    public string ServiceName { get; init; } = "WiFred Server";
}
