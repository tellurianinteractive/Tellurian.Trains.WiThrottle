using Makaretu.Dns;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiFreds.Configuration;

namespace Tellurian.Trains.WiFreds.Server;

/// <summary>
/// Advertises the WiFred server via mDNS so WiFred devices can discover it automatically.
/// </summary>
public sealed class MdnsAdvertiser(IOptions<WiFredSettings> settings, ILogger<MdnsAdvertiser> logger) : BackgroundService
{
    private readonly WiFredSettings _settings = settings.Value;
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
