namespace Tellurian.Trains.WiFreds.Server;

/// <summary>
/// Starts the command station adapter's receive loop on application startup.
/// </summary>
public sealed class CommandStationInitializer(IServiceProvider services, ILogger<CommandStationInitializer> logger) : BackgroundService
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<CommandStationInitializer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Try LocoNet adapter
        var locoNetAdapter = _services.GetService<Adapters.LocoNet.Adapter>();
        if (locoNetAdapter is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Starting LocoNet adapter receive loop");
            await locoNetAdapter.StartReceiveAsync(stoppingToken);
            return;
        }

        // Try Z21 adapter
        var z21Adapter = _services.GetService<Adapters.Z21.Adapter>();
        if (z21Adapter is not null)
        {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Starting Z21 adapter receive loop");
            await z21Adapter.StartReceiveAsync(stoppingToken);
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("No hardware adapter registered (Development mode)");
    }
}
