using Tellurian.Trains.WiFreds.Throttling;

namespace Tellurian.Trains.WiFreds.Tests.Throttling;

[TestClass]
public class GlobalRateLimiterTests
{
    [TestMethod]
    public void TryAcquire_InitiallyHasTokens()
    {
        var limiter = new GlobalRateLimiter(20);

        var result = limiter.TryAcquire();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void TryAcquire_DepletesBurstCapacity()
    {
        var limiter = new GlobalRateLimiter(5);

        // Drain all 5 tokens
        for (int i = 0; i < 5; i++)
            Assert.IsTrue(limiter.TryAcquire());

        // Next should fail
        var result = limiter.TryAcquire();
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task TryAcquire_RefillsOverTime()
    {
        var limiter = new GlobalRateLimiter(10);

        // Drain all tokens
        for (int i = 0; i < 10; i++)
            limiter.TryAcquire();

        Assert.IsFalse(limiter.TryAcquire());

        // Wait for refill (at 10/sec, 1 token per 100ms)
        await Task.Delay(150);

        Assert.IsTrue(limiter.TryAcquire());
    }

    [TestMethod]
    public async Task WaitForTokenAsync_EventuallyAcquires()
    {
        var limiter = new GlobalRateLimiter(10);

        // Drain all tokens
        for (int i = 0; i < 10; i++)
            limiter.TryAcquire();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Should not throw - a token will be acquired after refill
        await limiter.WaitForTokenAsync(cts.Token);
    }
}
