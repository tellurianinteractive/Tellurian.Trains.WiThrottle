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
    private readonly ConcurrentDictionary<IPAddress, Timer> _refreshTimers = new();
    private readonly ConcurrentDictionary<IPAddress, IReadOnlyList<string>> _pendingReEnable = new();

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

        var isNew = !_devices.ContainsKey(ip);
        var device = _devices.GetOrAdd(ip, addr => new WiFredDevice(addr));
        device.LastSeen = DateTimeOffset.UtcNow;
        device.IsActive = true;

        _ = FetchConfigurationAsync(device, cancellationToken);

        if (isNew)
            StartRefreshTimer(device);
    }

    private void HandleInactive(IPAddress ip)
    {
        if (_devices.TryGetValue(ip, out var device))
        {
            device.IsActive = false;
            StopRefreshTimer(ip);
            _pendingReEnable.TryRemove(ip, out _);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("wiFRED at {Address} ({Name}) marked inactive",
                    ip, device.Name ?? "(unnamed)");
        }
    }

    private void StartRefreshTimer(WiFredDevice device)
    {
        var interval = TimeSpan.FromMinutes(_settings.RefreshIntervalMinutes);
        var timer = new Timer(_ =>
        {
            if (device.IsActive)
                _ = FetchConfigurationAsync(device, CancellationToken.None);
        }, null, interval, interval);

        _refreshTimers[device.Address] = timer;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Started refresh timer for wiFRED at {Address} (every {Minutes} min)",
                device.Address, _settings.RefreshIntervalMinutes);
    }

    private void StopRefreshTimer(IPAddress ip)
    {
        if (_refreshTimers.TryRemove(ip, out var timer))
            timer.Dispose();
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

            await HandleExtraNetworksAsync(device);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(ex, "Failed to fetch configuration from wiFRED at {Address}", device.Address);
        }
    }

    /// <summary>
    /// Manages the wiFRED's WiFi network list to work around the firmware's stale mDNS cache.
    /// When multiple networks are enabled, the firmware may reuse a cached server address from
    /// a previously connected network. The fix is a two-phase process:
    /// <list type="number">
    ///   <item>Disable extra networks and restart — forces a fresh mDNS discovery on the current network.</item>
    ///   <item>After the wiFRED reconnects, re-enable the other networks so they are available next time.</item>
    /// </list>
    /// </summary>
    private async Task HandleExtraNetworksAsync(WiFredDevice device)
    {
        // Phase 2: Re-enable networks that were disabled in a previous cycle
        if (_pendingReEnable.TryRemove(device.Address, out var ssidsToReEnable))
        {
            await ReEnableNetworksAsync(device, ssidsToReEnable);
            return;
        }

        // Phase 1: Disable extra enabled networks and restart
        var extraNetworks = device.ExtraEnabledNetworks;
        if (extraNetworks.Count == 0)
            return;

        var ssids = extraNetworks.Select(n => n.Ssid).ToList();

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            foreach (var ssid in ssids)
            {
                await client.GetStringAsync($"http://{device.Address}/?disable={Uri.EscapeDataString(ssid)}");

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation(
                        "Disabled WiFi network \"{Ssid}\" on wiFRED {Name} at {Address} (keeping only \"{ConnectedSsid}\")",
                        ssid, device.Name ?? "(unnamed)", device.Address, device.ConnectedSsid);
            }

            // Remember which SSIDs to re-enable after the device reconnects
            _pendingReEnable[device.Address] = ssids;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Restarting wiFRED {Name} at {Address} to force fresh mDNS discovery",
                    device.Name ?? "(unnamed)", device.Address);

            await client.GetStringAsync($"http://{device.Address}/restart.html");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(ex, "Failed to disable extra WiFi networks on wiFRED at {Address}", device.Address);
        }
    }

    private async Task ReEnableNetworksAsync(WiFredDevice device, IReadOnlyList<string> ssids)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            foreach (var ssid in ssids)
            {
                await client.GetStringAsync($"http://{device.Address}/?enable={Uri.EscapeDataString(ssid)}");

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation(
                        "Re-enabled WiFi network \"{Ssid}\" on wiFRED {Name} at {Address}",
                        ssid, device.Name ?? "(unnamed)", device.Address);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(ex, "Failed to re-enable WiFi networks on wiFRED at {Address}", device.Address);
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

    public override void Dispose()
    {
        foreach (var timer in _refreshTimers.Values)
            timer.Dispose();
        _refreshTimers.Clear();
        base.Dispose();
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
