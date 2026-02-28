namespace Tellurian.Trains.WiThrottles.Configuration;

public sealed record CommandStationSettings
{
    public string Type { get; init; } = "";
    public SerialPortSettings SerialPort { get; init; } = new();
    public Z21Settings Z21 { get; init; } = new();
}

public sealed record SerialPortSettings
{
    public string PortName { get; init; } = "COM3";
    public int BaudRate { get; init; } = 57600;
}

public sealed record Z21Settings
{
    public string Address { get; init; } = "192.168.0.111";
    public int CommandPort { get; init; } = 21105;
    public int FeedbackPort { get; init; } = 21106;
}
