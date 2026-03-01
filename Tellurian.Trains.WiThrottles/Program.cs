using System.IO.Ports;
using System.Net;
using Tellurian.Trains.Adapters.LocoNet;
using Tellurian.Trains.Communications.Channels;
using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.Protocols.LocoNet;
using Tellurian.Trains.WiThrottles.Configuration;
using Tellurian.Trains.WiThrottles.Development;
using Tellurian.Trains.WiThrottles.Server;
using Tellurian.Trains.WiThrottles.Throttling;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<WiThrottleSettings>(builder.Configuration.GetSection("WiThrottle"));
builder.Services.Configure<ThrottlingSettings>(builder.Configuration.GetSection("Throttling"));
builder.Services.Configure<CommandStationSettings>(builder.Configuration.GetSection("CommandStation"));

// Command station integration
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ILoco, LoggingLocoController>();
}
else
{
    var csConfig = builder.Configuration.GetSection("CommandStation");
    var csType = csConfig["Type"];

    if (string.IsNullOrWhiteSpace(csType))
    {
        throw new InvalidOperationException(
            "CommandStation:Type is not configured. Set it to \"Serial\" or \"Z21\" in appsettings.json.");
    }

    if (csType.Equals("Z21", StringComparison.OrdinalIgnoreCase))
    {
        // Z21 via UDP
        builder.Services.AddSingleton<ICommunicationsChannel>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommandStationSettings>>().Value;
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(settings.Z21.Address), settings.Z21.CommandPort);
            var logger = sp.GetRequiredService<ILogger<UdpDataChannel>>();
            return new UdpDataChannel(settings.Z21.FeedbackPort, remoteEndPoint, logger);
        });

        builder.Services.AddSingleton<Tellurian.Trains.Adapters.Z21.Adapter>();
        builder.Services.AddSingleton<ILoco>(sp => sp.GetRequiredService<Tellurian.Trains.Adapters.Z21.Adapter>());
    }
    else if (csType.Equals("Serial", StringComparison.OrdinalIgnoreCase))
    {
        // LocoNet via serial port — verify the port exists before wiring up
        var portName = csConfig["SerialPort:PortName"] ?? "COM3";
        var availablePorts = SerialPort.GetPortNames();
        if (!availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Serial port \"{portName}\" is not available. Available ports: {(availablePorts.Length > 0 ? string.Join(", ", availablePorts) : "none")}. " +
                "Check CommandStation:SerialPort:PortName in appsettings.json.");
        }

        builder.Services.AddSingleton<IByteStreamFramer, LocoNetFramer>();
        builder.Services.AddSingleton<ISerialPortAdapter>(sp =>
        {
            var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CommandStationSettings>>().Value;
            return new SerialPortAdapter(settings.SerialPort.PortName, settings.SerialPort.BaudRate,
                Parity.None, 8, StopBits.One);
        });
        builder.Services.AddSingleton<ICommunicationsChannel, SerialDataChannel>();

        builder.Services.AddSingleton<Adapter>();
        builder.Services.AddSingleton<ILoco>(sp => sp.GetRequiredService<Adapter>());
    }
    else
    {
        throw new InvalidOperationException(
            $"Unknown CommandStation:Type \"{csType}\". Supported values are \"Serial\" and \"Z21\".");
    }

    builder.Services.AddHostedService<CommandStationInitializer>();
}

// Throttling
builder.Services.AddSingleton<ThrottledLocoController>();

// Server
builder.Services.AddHostedService<WiThrottleTcpServer>();
builder.Services.AddHostedService<MdnsAdvertiser>();

var host = builder.Build();
host.Run();
