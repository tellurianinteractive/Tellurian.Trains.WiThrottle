namespace Tellurian.Trains.WiFreds.Configuration;

public sealed record CommandStationSettings
{
    public string Type { get; init; } = "";
    public SerialPortSettings SerialPort { get; init; } = new();
    public Z21Settings Z21 { get; init; } = new();
    public LocoNetTcpSettings LocoNetTcp { get; init; } = new();
    public LocoNetUdpSettings LocoNetUdp { get; init; } = new();
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

public sealed record LocoNetTcpSettings
{
    public string Hostname { get; init; } = "localhost";
    public int Port { get; init; } = 1234;
}

public sealed record LocoNetUdpSettings
{
    public string MulticastGroup { get; init; } = "224.0.0.1";
    public int ListenPort { get; init; } = 1235;
    public string SendAddress { get; init; } = "224.0.0.1";
    public int SendPort { get; init; } = 1235;
    public bool ValidateChecksum { get; init; } = true;
}
