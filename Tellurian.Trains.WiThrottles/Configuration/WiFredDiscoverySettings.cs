namespace Tellurian.Trains.WiThrottles.Configuration;

public sealed record WiFredDiscoverySettings
{
    public int UdpPort { get; init; } = 51289;
}
