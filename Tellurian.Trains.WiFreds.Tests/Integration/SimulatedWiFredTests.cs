using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tellurian.Trains.WiFreds.Configuration;
using Tellurian.Trains.WiFreds.Server;
using Tellurian.Trains.WiFreds.Tests.Helpers;
using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Tests.Integration;

[TestClass]
public class SimulatedWiFredTests
{
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static (WiFredTcpServer Server, RecordingLocoController Recorder, int Port) CreateServer()
    {
        var port = GetFreePort();
        var recorder = new RecordingLocoController();
        var throttlingSettings = Options.Create(new ThrottlingSettings
        {
            SpeedTimeThresholdMs = 0,
            SpeedStepThreshold = 0,
            GlobalMessageRatePerSecond = 1000
        });
        var controller = new ThrottledLocoController(
            recorder,
            throttlingSettings,
            NullLogger<ThrottledLocoController>.Instance);

        var serverSettings = Options.Create(new WiFredSettings
        {
            Port = port,
            HeartbeatTimeoutSeconds = 5,
            ServiceName = "Test Server"
        });

        var server = new WiFredTcpServer(
            serverSettings,
            controller,
            NullLoggerFactory.Instance,
            NullLogger<WiFredTcpServer>.Instance);

        return (server, recorder, port);
    }

    [TestMethod]
    public async Task FullLifecycle_ConnectAcquireDriveQuit()
    {
        var (server, recorder, port) = CreateServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = server.StartAsync(cts.Token);

        await Task.Delay(200); // Let server start listening

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            // Read handshake
            var line1 = await reader.ReadLineAsync(cts.Token);
            Assert.AreEqual("VN2.0", line1);
            var line2 = await reader.ReadLineAsync(cts.Token);
            Assert.AreEqual("*5", line2);

            // Send throttle name and hardware ID
            await writer.WriteLineAsync("NTestWiFred");
            await writer.WriteLineAsync("HU112233445566");
            await writer.WriteLineAsync("*+");

            // Acquire a loco
            await writer.WriteLineAsync("MT+L1234<;>L1234");

            // Read acquisition response until s128 end marker
            var responseLines = new List<string>();
            while (true)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is null) break;
                responseLines.Add(line);
                if (line.Contains("s128")) break;
            }

            Assert.HasCount(31, responseLines); // 29 functions + direction + speed steps
            Assert.StartsWith("MTAL1234<;>F", responseLines[0]);
            Assert.Contains("s128", responseLines[30]);

            // Send speed command
            await writer.WriteLineAsync("MTAL1234<;>V50");
            await Task.Delay(100);

            // Send direction command
            await writer.WriteLineAsync("MTAL1234<;>R0");
            await Task.Delay(100);

            // Send function command
            await writer.WriteLineAsync("MTAL1234<;>F10");
            await Task.Delay(100);

            // Quit
            await writer.WriteLineAsync("Q");
            await Task.Delay(200);

            // Verify commands were forwarded
            Assert.IsTrue(recorder.DriveCalls.Any(), "Expected drive calls");
            Assert.IsTrue(recorder.SetFunctionCalls.Any(), "Expected function calls");
            Assert.IsTrue(recorder.EmergencyStopCalls.Any(), "Expected e-stop on quit");
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [TestMethod]
    public async Task HeartbeatTimeout_TriggersEmergencyStop()
    {
        var port = GetFreePort();
        var recorder = new RecordingLocoController();
        var throttlingSettings = Options.Create(new ThrottlingSettings
        {
            SpeedTimeThresholdMs = 0,
            SpeedStepThreshold = 0,
            GlobalMessageRatePerSecond = 1000
        });
        var controller = new ThrottledLocoController(
            recorder,
            throttlingSettings,
            NullLogger<ThrottledLocoController>.Instance);

        var serverSettings = Options.Create(new WiFredSettings
        {
            Port = port,
            HeartbeatTimeoutSeconds = 2, // Short timeout for testing
            ServiceName = "Test Server"
        });

        var server = new WiFredTcpServer(
            serverSettings,
            controller,
            NullLoggerFactory.Instance,
            NullLogger<WiFredTcpServer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(200);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            // Read handshake
            await reader.ReadLineAsync(cts.Token);
            await reader.ReadLineAsync(cts.Token);

            // Enable heartbeat and acquire loco
            await writer.WriteLineAsync("NHeartbeatTest");
            await writer.WriteLineAsync("*+");
            await writer.WriteLineAsync("MT+L1234<;>L1234");

            // Drain acquisition response (read until s128 marker)
            while (true)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is null || line.Contains("s128")) break;
            }

            // Stop sending heartbeats and wait for timeout
            recorder.Calls.Clear();
            await Task.Delay(4000); // Wait > 2x heartbeat timeout

            // Verify emergency stop was called due to heartbeat timeout
            Assert.IsTrue(recorder.EmergencyStopCalls.Any(), "Expected e-stop from heartbeat timeout");
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }

    [TestMethod]
    public async Task MultipleLocos_WildcardSpeed_AffectsAll()
    {
        var (server, recorder, port) = CreateServer();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = server.StartAsync(cts.Token);
        await Task.Delay(200);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);

            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            await using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

            // Read handshake
            await reader.ReadLineAsync(cts.Token);
            await reader.ReadLineAsync(cts.Token);

            // Acquire 4 locos
            await writer.WriteLineAsync("NMultiTest");
            for (int i = 1; i <= 4; i++)
            {
                await writer.WriteLineAsync($"MT+L{i}<;>L{i}");
                // Drain acquisition response (read until s128 marker)
                while (true)
                {
                    var line = await reader.ReadLineAsync(cts.Token);
                    if (line is null || line.Contains("s128")) break;
                }
            }

            recorder.Calls.Clear();

            // Send wildcard speed command
            await writer.WriteLineAsync("MTA*<;>V75");
            await Task.Delay(200);

            // All 4 locos should have received a drive command
            Assert.AreEqual(4, recorder.DriveCalls.Count(), "Expected 4 drive calls for wildcard speed");

            // Quit
            await writer.WriteLineAsync("Q");
            await Task.Delay(200);
        }
        finally
        {
            cts.Cancel();
            try { await serverTask; } catch (OperationCanceledException) { }
        }
    }
}
