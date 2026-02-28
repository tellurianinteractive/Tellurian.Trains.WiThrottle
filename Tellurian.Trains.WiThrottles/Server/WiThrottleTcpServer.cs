using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiThrottles.Configuration;
using Tellurian.Trains.WiThrottles.Protocol;
using Tellurian.Trains.WiThrottles.Sessions;
using Tellurian.Trains.WiThrottles.Throttling;

namespace Tellurian.Trains.WiThrottles.Server;

/// <summary>
/// TCP server that accepts WiThrottle client connections and manages per-client sessions.
/// </summary>
public sealed class WiThrottleTcpServer : BackgroundService
{
    private readonly WiThrottleSettings _settings;
    private readonly ThrottledLocoController _controller;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WiThrottleTcpServer> _logger;
    private readonly ConcurrentDictionary<string, SessionHandler> _activeSessions = new();

    public WiThrottleTcpServer(
        IOptions<WiThrottleSettings> settings,
        ThrottledLocoController controller,
        ILoggerFactory loggerFactory,
        ILogger<WiThrottleTcpServer> logger)
    {
        _settings = settings.Value;
        _controller = controller;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _settings.Port);
        listener.Start();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("WiThrottle server listening on port {Port}", _settings.Port);

        // Start heartbeat monitor
        _ = MonitorHeartbeatsAsync(stoppingToken);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stoppingToken);
                var clientId = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Client connected: {ClientId}", clientId);

                _ = HandleClientAsync(client, clientId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        finally
        {
            listener.Stop();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("WiThrottle server stopped");
        }
    }

    private async Task HandleClientAsync(TcpClient client, string clientId, CancellationToken stoppingToken)
    {
        var session = new ThrottleSession();
        var sessionLogger = _loggerFactory.CreateLogger($"WiThrottle.Session.{clientId}");
        var handler = new SessionHandler(session, _controller, sessionLogger);
        _activeSessions[clientId] = handler;

        try
        {
            using (client)
            await using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, leaveOpen: true))
            await using (var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true })
            {
                // Send handshake
                await writer.WriteAsync($"VN2.0\n*{_settings.HeartbeatTimeoutSeconds}\n");

                // Read loop
                while (!stoppingToken.IsCancellationRequested)
                {
                    string? line;
                    try
                    {
                        line = await reader.ReadLineAsync(stoppingToken);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (line is null) break; // Client disconnected

                    var message = WiThrottleParser.Parse(line);
                    var response = await handler.HandleAsync(message, stoppingToken);

                    if (response is not null)
                    {
                        // Write atomically to avoid interleaving between clients
                        await writer.WriteAsync(response);
                    }

                    if (message is WiThrottleMessage.Quit) break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Server shutting down
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError(ex, "Error handling client {ClientId}", clientId);
        }
        finally
        {
            // Cleanup: e-stop all locos on disconnect
            try
            {
                await handler.EmergencyStopAllAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError(ex, "Error during disconnect cleanup for {ClientId}", clientId);
            }

            _activeSessions.TryRemove(clientId, out _);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Client disconnected: {ClientId} ({Name})", clientId, session.Name);
        }
    }

    private async Task MonitorHeartbeatsAsync(CancellationToken stoppingToken)
    {
        var checkInterval = TimeSpan.FromSeconds(Math.Max(1, _settings.HeartbeatTimeoutSeconds / 2.0));

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, stoppingToken);

            var timeout = TimeSpan.FromSeconds(_settings.HeartbeatTimeoutSeconds);
            var now = DateTimeOffset.UtcNow;

            foreach (var (clientId, handler) in _activeSessions)
            {
                var session = handler.Session;
                if (!session.HeartbeatEnabled) continue;

                if (now - session.LastActivity > timeout)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("Heartbeat timeout for client {ClientId} ({Name}), emergency stopping all locos",
                            clientId, session.Name);

                    try
                    {
                        await handler.EmergencyStopAllAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        if (_logger.IsEnabled(LogLevel.Error))
                            _logger.LogError(ex, "Error during heartbeat timeout e-stop for {ClientId}", clientId);
                    }

                    // Reset activity to avoid repeated e-stops
                    session.TouchActivity();
                }
            }
        }
    }
}
