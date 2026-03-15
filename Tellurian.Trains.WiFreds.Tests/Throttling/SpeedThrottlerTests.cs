using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Tests.Throttling;

[TestClass]
public class SpeedThrottlerTests
{
    [TestMethod]
    public async Task SubmitSpeed0_AlwaysForwardedImmediately()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(1000, 100, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        var result = await throttler.SubmitAsync(0);

        Assert.IsTrue(result);
        Assert.HasCount(1, forwarded);
        Assert.AreEqual((byte)0, forwarded[0]);
    }

    [TestMethod]
    public async Task SubmitSpeed_FirstCall_ForwardedImmediately()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(200, 5, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        var result = await throttler.SubmitAsync(50);

        Assert.IsTrue(result);
        Assert.HasCount(1, forwarded);
        Assert.AreEqual((byte)50, forwarded[0]);
    }

    [TestMethod]
    public async Task SubmitSpeed_WithinThresholds_Suppressed()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(500, 10, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        await throttler.SubmitAsync(50); // First call always forwards
        var result = await throttler.SubmitAsync(51); // Within both thresholds

        Assert.IsFalse(result);
        Assert.HasCount(1, forwarded); // Only the first one
    }

    [TestMethod]
    public async Task SubmitSpeed_StepThresholdExceeded_Forwarded()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(5000, 5, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        await throttler.SubmitAsync(50); // First call
        var result = await throttler.SubmitAsync(60); // Step change of 10 > threshold 5

        Assert.IsTrue(result);
        Assert.HasCount(2, forwarded);
        Assert.AreEqual((byte)60, forwarded[1]);
    }

    [TestMethod]
    public async Task SubmitSpeed_TimeThresholdExceeded_Forwarded()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(50, 100, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        await throttler.SubmitAsync(50); // First call
        await Task.Delay(200); // Wait well beyond time threshold
        var result = await throttler.SubmitAsync(51); // Small step but time exceeded

        Assert.IsTrue(result);
        Assert.HasCount(2, forwarded);
    }

    [TestMethod]
    public async Task TrailingEdge_ForwardsPendingValue()
    {
        var forwarded = new List<byte>();
        using var throttler = new SpeedThrottler(100, 2, speed =>
        {
            forwarded.Add(speed);
            return Task.CompletedTask;
        });

        // First call: step change 50 > threshold 2, so it forwards immediately
        var first = await throttler.SubmitAsync(50);
        Assert.IsTrue(first);

        // Second call immediately after: step change 1, elapsed ~0ms, both below thresholds
        var second = await throttler.SubmitAsync(51);
        Assert.IsFalse(second);

        // Wait for trailing edge timer to fire the pending value
        await Task.Delay(500);

        Assert.HasCount(2, forwarded);
        Assert.AreEqual((byte)51, forwarded[1]);
    }
}
