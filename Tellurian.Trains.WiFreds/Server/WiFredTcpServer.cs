using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiFreds.Configuration;
using Tellurian.Trains.WiFreds.Protocol;
using Tellurian.Trains.WiFreds.Sessions;
using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Server;

/// <summary>
/// TCP server that accepts WiFred client connections and manages per-client sessions.
/// </summary>
public sealed class WiFredTcpServer : BackgroundService
{
    private readonly WiFredSettings _settings;
    private readonly ThrottledLocoController _controller;
    private readonly ActiveLocoTracker _tracker;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WiFredTcpServer> _logger;
    private readonly ConcurrentDictionary<string, SessionHandler> _activeSessions = new();

    public WiFredTcpServer(
        IOptions<WiFredSettings> settings,
        ThrottledLocoController controller,
        ActiveLocoTracker tracker,
        ILoggerFactory loggerFactory,
        ILogger<WiFredTcpServer> logger)
    {
        _settings = settings.Value;
        _controller = controller;
        _tracker = tracker;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _settings.Port);
        listener.Start();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("WiFred server listening on port {Port}", _settings.Port);

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
                _logger.LogInformation("WiFred server stopped");
        }
    }

    private async Task HandleClientAsync(TcpClient client, string clientId, CancellationToken stoppingToken)
    {
        var session = new ThrottleSession();
        var sessionLogger = _loggerFactory.CreateLogger($"WiFred.Session.{clientId}");
        var handler = new SessionHandler(session, _controller, _tracker, clientId, sessionLogger);
        _activeSessions[clientId] = handler;
        var clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address;

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

                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug("Received from {ClientId}: {Line}", clientId, line);

                    var message = WiFredParser.Parse(line);
                    var response = await handler.HandleAsync(message, stoppingToken);

                    if (response is not null)
                    {
                        // Write atomically to avoid interleaving between clients
                        await writer.WriteAsync(response);
                    }

                    if (message is WiFredMessage.Quit) break;
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
            // Cleanup: e-stop all locos and release from tracker on disconnect
            try
            {
                await handler.EmergencyStopAndReleaseAllAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.LogError(ex, "Error during disconnect cleanup for {ClientId}", clientId);
            }

            _activeSessions.TryRemove(clientId, out _);

            if (clientIp is not null)
            {
                try { await WiFredDiscoveryService.SendInactiveAsync(clientIp); }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning(ex, "Failed to send wiFRED inactive notification for {ClientId}", clientId);
                }
            }

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
