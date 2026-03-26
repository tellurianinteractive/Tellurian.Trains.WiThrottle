namespace Tellurian.Trains.WiFreds.Server;

/// <summary>
/// Starts the command station adapter's receive loop on application startup.
/// Automatically reconnects if the connection is lost (e.g. USB adapter unplugged).
/// </summary>
public sealed class CommandStationInitializer(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<CommandStationInitializer> logger) : BackgroundService
{
    private const int MaxRetries = 2;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _services = services;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ILogger<CommandStationInitializer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Try LocoNet adapter
        var locoNetAdapter = _services.GetService<Adapters.LocoNet.Adapter>();
        if (locoNetAdapter is not null)
        {
            await RunWithReconnectAsync("LocoNet",
                ct => locoNetAdapter.StartReceiveAsync(ct), stoppingToken);
            return;
        }

        // Try Z21 adapter
        var z21Adapter = _services.GetService<Adapters.Z21.Adapter>();
        if (z21Adapter is not null)
        {
            await RunWithReconnectAsync("Z21",
                ct => z21Adapter.StartReceiveAsync(ct), stoppingToken);
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("No hardware adapter registered (Development mode)");
    }

    private async Task RunWithReconnectAsync(string adapterName,
        Func<CancellationToken, Task> startReceive, CancellationToken stoppingToken)
    {
        var retries = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Starting {Adapter} adapter receive loop", adapterName);

                await startReceive(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase))
            {
                retries++;
                _logger.LogWarning("Connection to {Adapter} lost (port closed). Attempt {Attempt} of {Max}",
                    adapterName, retries, MaxRetries);
            }
            catch (System.IO.IOException ex)
            {
                retries++;
                _logger.LogWarning("Connection to {Adapter} lost: {Message}. Attempt {Attempt} of {Max}",
                    adapterName, ex.Message, retries, MaxRetries);
            }
            catch (Exception ex)
            {
                retries++;
                _logger.LogError(ex, "{Adapter} adapter failed unexpectedly. Attempt {Attempt} of {Max}",
                    adapterName, retries, MaxRetries);
            }

            if (retries >= MaxRetries)
            {
                _logger.LogCritical(
                    "Failed to connect to {Adapter} after {Max} attempts. Shutting down.",
                    adapterName, MaxRetries);
                _lifetime.StopApplication();
                return;
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
