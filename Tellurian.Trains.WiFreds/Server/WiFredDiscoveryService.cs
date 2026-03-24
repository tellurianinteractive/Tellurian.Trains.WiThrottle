using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiFreds.Configuration;

namespace Tellurian.Trains.WiFreds.Server;

public sealed class WiFredDiscoveryService(
    IOptions<WiFredDiscoverySettings> settings,
    IHttpClientFactory httpClientFactory,
    ILogger<WiFredDiscoveryService> logger) : BackgroundService
{
    private const string BroadcastPayload = "wiFred";
    private const string InactivePrefix = "wiFred-inactive:";

    private readonly WiFredDiscoverySettings _settings = settings.Value;
    private readonly ILogger<WiFredDiscoveryService> _logger = logger;
    private readonly ConcurrentDictionary<IPAddress, WiFredDevice> _devices = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udpClient = new UdpClient(_settings.UdpPort);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("wiFRED discovery listening on UDP port {Port}", _settings.UdpPort);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await udpClient.ReceiveAsync(stoppingToken);
                var payload = Encoding.UTF8.GetString(result.Buffer);

                if (string.Equals(payload, BroadcastPayload, StringComparison.Ordinal))
                {
                    HandleBroadcast(result.RemoteEndPoint.Address, stoppingToken);
                }
                else if (payload.StartsWith(InactivePrefix, StringComparison.Ordinal)
                    && IPAddress.TryParse(payload.AsSpan(InactivePrefix.Length), out var inactiveIp))
                {
                    HandleInactive(inactiveIp);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "wiFRED discovery failed");
        }
    }

    private void HandleBroadcast(IPAddress ip, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("wiFRED broadcast received from {Address}", ip);

        var device = _devices.GetOrAdd(ip, addr => new WiFredDevice(addr));
        device.LastSeen = DateTimeOffset.UtcNow;
        device.IsActive = true;

        _ = FetchConfigurationAsync(device, cancellationToken);
    }

    private void HandleInactive(IPAddress ip)
    {
        if (_devices.TryGetValue(ip, out var device))
        {
            device.IsActive = false;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("wiFRED at {Address} ({Name}) marked inactive",
                    ip, device.Name ?? "(unnamed)");
        }
    }

    private async Task FetchConfigurationAsync(WiFredDevice device, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var xml = await client.GetStringAsync($"http://{device.Address}/api/getConfigXML", cancellationToken);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Raw XML from wiFRED at {Address}: {Xml}", device.Address, xml);
            xml = NormalizeXmlDeclaration(xml);
            device.Configuration = XDocument.Parse(xml);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Fetched configuration for wiFRED {Name} at {Address}",
                    device.Name ?? "(unnamed)", device.Address);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(ex, "Failed to fetch configuration from wiFRED at {Address}", device.Address);
        }
    }

    public async Task RefreshConfigurationAsync(IPAddress deviceAddress)
    {
        if (_devices.TryGetValue(deviceAddress, out var device))
            await FetchConfigurationAsync(device, CancellationToken.None);
    }

    public IReadOnlyList<WiFredDevice> GetDiscoveredDevices()
    {
        return _devices.Values
            .Where(d => d.IsActive)
            .ToList();
    }

    public IReadOnlyList<LocoAddressConflict> GetLocoAddressConflicts()
    {
        var devices = GetDiscoveredDevices();
        return devices
            .SelectMany(d => d.LocoAddresses.Select(a => (Address: a, Device: d)))
            .GroupBy(x => x.Address)
            .Where(g => g.Count() > 1)
            .Select(g => new LocoAddressConflict(g.Key, g.Select(x => x.Device).ToList()))
            .ToList();
    }

    public async Task<bool> UpdateLocoAddressAsync(IPAddress deviceAddress, int slot, int newAddress)
    {
        if (!_devices.TryGetValue(deviceAddress, out var device))
            return false;

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var url = $"http://{deviceAddress}/?loco={slot}&loco.address={newAddress}";
            if (newAddress > 127)
                url += "&loco.longAddress=1";
            await client.GetStringAsync(url);
            await FetchConfigurationAsync(device, CancellationToken.None);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Updated loco {Slot} address to {Address} on wiFRED at {DeviceAddress}",
                    slot, newAddress, deviceAddress);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(ex, "Failed to update loco address on wiFRED at {Address}", deviceAddress);
            return false;
        }
    }

    /// <summary>
    /// The wiFRED firmware emits &lt;?XML ...?&gt; (uppercase) which violates the XML spec.
    /// Replace it with the correct lowercase &lt;?xml ...?&gt; so XDocument.Parse succeeds.
    /// </summary>
    private static string NormalizeXmlDeclaration(string xml) =>
        Regex.Replace(xml, @"<\?XML\s", "<?xml ", RegexOptions.IgnoreCase);

    /// <summary>
    /// Sends a UDP message to mark a wiFRED device as inactive.
    /// Call this from the TCP server when a client disconnects.
    /// </summary>
    public static async Task SendInactiveAsync(IPAddress deviceAddress, int udpPort = 51289)
    {
        using var client = new UdpClient();
        var payload = Encoding.UTF8.GetBytes($"{InactivePrefix}{deviceAddress}");
        await client.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, udpPort));
    }
}
