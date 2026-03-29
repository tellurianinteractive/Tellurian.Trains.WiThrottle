using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiFreds.Configuration;
using Tellurian.Trains.WiFreds.Protocol;
using Tellurian.Trains.WiFreds.Sessions;
using Tellurian.Trains.WiFreds.Tests.Helpers;
using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Tests.Sessions;

[TestClass]
public class SessionHandlerTests
{
    private static (SessionHandler Handler, RecordingLocoController Recorder, ActiveLocoTracker Tracker) CreateHandler()
    {
        var recorder = new RecordingLocoController();
        var settings = Options.Create(new ThrottlingSettings
        {
            SpeedTimeThresholdMs = 0,  // No debouncing in unit tests
            SpeedStepThreshold = 0,
            GlobalMessageRatePerSecond = 1000
        });
        var controller = new ThrottledLocoController(
            recorder,
            settings,
            NullLogger<ThrottledLocoController>.Instance);
        var session = new ThrottleSession();
        var tracker = new ActiveLocoTracker(NullLogger<ActiveLocoTracker>.Instance);
        var handler = new SessionHandler(session, controller, tracker, "test-session", NullLogger.Instance);
        return (handler, recorder, tracker);
    }

    private static async Task AcquireLocoAsync(SessionHandler handler, string locoId)
    {
        await handler.HandleAsync(new WiFredMessage.AcquireLoco(locoId));
    }

    [TestMethod]
    public async Task AcquireLoco_ReturnsMultiLineResponse()
    {
        var (handler, _, _) = CreateHandler();

        var response = await handler.HandleAsync(new WiFredMessage.AcquireLoco("L1234"));

        Assert.IsNotNull(response);
        // Should contain function states F0-F28 (29 lines), direction, speed steps
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(31, lines); // 29 functions + direction + speed step mode
        Assert.StartsWith("MTAL1234<;>F", lines[0]);
        Assert.Contains("R1", lines[29]); // Default forward direction
        Assert.Contains("s128", lines[30]);
    }

    [TestMethod]
    public async Task AcquireLoco_InvalidAddress_ReturnsNull()
    {
        var (handler, _, _) = CreateHandler();

        var response = await handler.HandleAsync(new WiFredMessage.AcquireLoco("INVALID"));

        Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SetSpeed_CallsDriveWithSpeedThrottling()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.SetSpeed("L1234", 50));

        // With threshold=0, speed should be forwarded immediately via the throttler
        await Task.Delay(50); // Allow async callback to complete
        Assert.IsTrue(recorder.DriveCalls.Any());
        var call = recorder.DriveCalls.First();
        Assert.AreEqual(1234, call.Address.Number);
    }

    [TestMethod]
    public async Task SetDirection_CallsDriveAsync()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.SetDirection("L1234", false));

        Assert.AreEqual(1, recorder.DriveCalls.Count());
        var call = recorder.DriveCalls.First();
        Assert.AreEqual(1234, call.Address.Number);
        Assert.AreEqual(Direction.Backward, call.Drive!.Value.Direction);
    }

    [TestMethod]
    public async Task EmergencyStop_SingleLoco_CallsEmergencyStop()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.EmergencyStop("L1234"));

        Assert.AreEqual(1, recorder.EmergencyStopCalls.Count());
        Assert.AreEqual(1234, recorder.EmergencyStopCalls.First().Address.Number);
    }

    [TestMethod]
    public async Task EmergencyStop_Wildcard_StopsAllLocos()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");
        await AcquireLocoAsync(handler, "S5");

        await handler.HandleAsync(new WiFredMessage.EmergencyStop("*"));

        Assert.AreEqual(2, recorder.EmergencyStopCalls.Count());
    }

    [TestMethod]
    public async Task SetFunction_LatchingPress_TogglesOn()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.SetFunction("L1234", 0, true, false));

        Assert.AreEqual(1, recorder.SetFunctionCalls.Count());
        var call = recorder.SetFunctionCalls.First();
        Assert.AreEqual(1234, call.Address.Number);
        Assert.IsTrue(call.Function!.Value.IsOn);
    }

    [TestMethod]
    public async Task SetFunction_LatchingRelease_IsIgnored()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.SetFunction("L1234", 0, false, false));

        Assert.AreEqual(0, recorder.SetFunctionCalls.Count());
    }

    [TestMethod]
    public async Task SetFunction_LatchingPressAgain_TogglesOff()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        // First press: toggle off -> on
        await handler.HandleAsync(new WiFredMessage.SetFunction("L1234", 0, true, false));
        // Second press: toggle on -> off
        await handler.HandleAsync(new WiFredMessage.SetFunction("L1234", 0, true, false));

        Assert.AreEqual(2, recorder.SetFunctionCalls.Count());
        Assert.IsTrue(recorder.SetFunctionCalls.First().Function!.Value.IsOn);
        Assert.IsFalse(recorder.SetFunctionCalls.Last().Function!.Value.IsOn);
    }

    [TestMethod]
    public async Task SetFunction_Force_DirectlySetsState()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.HandleAsync(new WiFredMessage.SetFunction("L1234", 3, false, true));

        Assert.AreEqual(1, recorder.SetFunctionCalls.Count());
        var call = recorder.SetFunctionCalls.First();
        Assert.IsFalse(call.Function!.Value.IsOn);
    }

    [TestMethod]
    public async Task Quit_EmergencyStopsAllLocos()
    {
        var (handler, recorder, _) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");
        await AcquireLocoAsync(handler, "S5");

        await handler.HandleAsync(new WiFredMessage.Quit());

        Assert.AreEqual(2, recorder.EmergencyStopCalls.Count());
    }

    [TestMethod]
    public async Task ThrottleName_SetsSessionName()
    {
        var (handler, _, _) = CreateHandler();

        await handler.HandleAsync(new WiFredMessage.ThrottleName("MyWiFred"));

        Assert.AreEqual("MyWiFred", handler.Session.Name);
    }

    [TestMethod]
    public async Task HardwareId_SetsSessionHardwareId()
    {
        var (handler, _, _) = CreateHandler();

        await handler.HandleAsync(new WiFredMessage.HardwareId("aabbccddee"));

        Assert.AreEqual("aabbccddee", handler.Session.HardwareId);
    }

    [TestMethod]
    public async Task HeartbeatOptIn_EnablesHeartbeat()
    {
        var (handler, _, _) = CreateHandler();

        await handler.HandleAsync(new WiFredMessage.HeartbeatOptIn());

        Assert.IsTrue(handler.Session.HeartbeatEnabled);
    }

    [TestMethod]
    public async Task HandleAsync_TouchesActivity()
    {
        var (handler, _, _) = CreateHandler();
        var before = handler.Session.LastActivity;
        Thread.Sleep(10);

        await handler.HandleAsync(new WiFredMessage.Heartbeat());

        Assert.IsTrue(handler.Session.LastActivity > before);
    }

    [TestMethod]
    public async Task AcquireLoco_MarksActiveInTracker()
    {
        var (handler, _, tracker) = CreateHandler();

        await handler.HandleAsync(new WiFredMessage.AcquireLoco("L1234"));

        Assert.IsTrue(tracker.IsActive(1234), "Acquired loco should be active in tracker");
    }

    [TestMethod]
    public async Task Quit_ReleasesLocosFromTracker()
    {
        var (handler, _, tracker) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");
        await AcquireLocoAsync(handler, "S5");
        Assert.IsTrue(tracker.IsActive(1234));
        Assert.IsTrue(tracker.IsActive(5));

        await handler.HandleAsync(new WiFredMessage.Quit());

        Assert.IsFalse(tracker.IsActive(1234), "Loco should be released from tracker after quit");
        Assert.IsFalse(tracker.IsActive(5), "Loco should be released from tracker after quit");
    }

    [TestMethod]
    public async Task EmergencyStopAll_DoesNotReleaseFromTracker()
    {
        var (handler, _, tracker) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");
        await AcquireLocoAsync(handler, "S5");

        await handler.EmergencyStopAllAsync();

        Assert.IsTrue(tracker.IsActive(1234), "E-stop must not release locos from tracker");
        Assert.IsTrue(tracker.IsActive(5), "E-stop must not release locos from tracker");
    }

    [TestMethod]
    public async Task EmergencyStopAndReleaseAll_ReleasesFromTracker()
    {
        var (handler, _, tracker) = CreateHandler();
        await AcquireLocoAsync(handler, "L1234");

        await handler.EmergencyStopAndReleaseAllAsync();

        Assert.IsFalse(tracker.IsActive(1234), "Release should remove locos from tracker");
    }

    [TestMethod]
    public async Task HeartbeatRecovery_ReAcquiresLocosInTracker()
    {
        var (handler, _, tracker) = CreateHandler();
        await handler.HandleAsync(new WiFredMessage.HeartbeatOptIn());
        await AcquireLocoAsync(handler, "L1234");
        Assert.IsTrue(tracker.IsActive(1234));

        // Simulate heartbeat timeout: e-stop without release, disable heartbeat
        await handler.EmergencyStopAllAsync();
        handler.Session.HeartbeatEnabled = false;

        // Locos should still be active since we only e-stopped, not released
        Assert.IsTrue(tracker.IsActive(1234), "E-stop must not release locos from tracker");

        // Simulate heartbeat recovery
        await handler.HandleAsync(new WiFredMessage.Heartbeat());

        Assert.IsTrue(handler.Session.HeartbeatEnabled, "Heartbeat should be re-enabled after recovery");
        Assert.IsTrue(tracker.IsActive(1234), "Locos should remain active after recovery");
    }
}
