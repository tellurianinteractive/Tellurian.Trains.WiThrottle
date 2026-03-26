namespace Tellurian.Trains.WiFreds.Configuration;

public sealed record WiFredDiscoverySettings
{
    public int UdpPort { get; init; } = 51289;
    public int RefreshIntervalMinutes { get; init; } = 5;
}
