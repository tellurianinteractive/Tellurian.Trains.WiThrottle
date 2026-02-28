namespace Tellurian.Trains.WiThrottles.Configuration;

public sealed record ThrottlingSettings
{
    public int SpeedTimeThresholdMs { get; init; } = 150;
    public int SpeedStepThreshold { get; init; } = 2;
    public int GlobalMessageRatePerSecond { get; init; } = 20;
}
