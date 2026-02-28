using Makaretu.Dns;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiThrottles.Configuration;

namespace Tellurian.Trains.WiThrottles.Server;

/// <summary>
/// Advertises the WiThrottle server via mDNS so WiFred devices can discover it automatically.
/// </summary>
public sealed class MdnsAdvertiser(IOptions<WiThrottleSettings> settings, ILogger<MdnsAdvertiser> logger) : BackgroundService
{
    private readonly WiThrottleSettings _settings = settings.Value;
    private readonly ILogger<MdnsAdvertiser> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var profile = new ServiceProfile(
                _settings.ServiceName,
                "_withrottle._tcp",
                (ushort)_settings.Port);

            var sd = new ServiceDiscovery();
            sd.Advertise(profile);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation(
                    "mDNS: Advertising {ServiceName} as _withrottle._tcp on port {Port}",
                    _settings.ServiceName, _settings.Port);

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "mDNS advertisement failed. WiFred devices will need manual IP configuration.");
        }
    }
}
