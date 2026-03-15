using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiFreds.Sessions;

namespace Tellurian.Trains.WiFreds.Tests.Sessions;

[TestClass]
public class ThrottleSessionTests
{
    [TestMethod]
    public void TryAddLoco_FirstLoco_ReturnsTrue()
    {
        var session = new ThrottleSession();
        var loco = new LocoState(Address.From(1234), "L1234");

        var result = session.TryAddLoco(loco);

        Assert.IsTrue(result);
        Assert.HasCount(1, session.Locos);
    }

    [TestMethod]
    public void TryAddLoco_FourLocos_AllSucceed()
    {
        var session = new ThrottleSession();

        Assert.IsTrue(session.TryAddLoco(new LocoState(Address.From(1), "S1")));
        Assert.IsTrue(session.TryAddLoco(new LocoState(Address.From(2), "S2")));
        Assert.IsTrue(session.TryAddLoco(new LocoState(Address.From(3), "S3")));
        Assert.IsTrue(session.TryAddLoco(new LocoState(Address.From(4), "S4")));

        Assert.HasCount(4, session.Locos);
    }

    [TestMethod]
    public void TryAddLoco_FifthLoco_ReturnsFalse()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1), "S1"));
        session.TryAddLoco(new LocoState(Address.From(2), "S2"));
        session.TryAddLoco(new LocoState(Address.From(3), "S3"));
        session.TryAddLoco(new LocoState(Address.From(4), "S4"));

        var result = session.TryAddLoco(new LocoState(Address.From(5), "S5"));

        Assert.IsFalse(result);
        Assert.HasCount(4, session.Locos);
    }

    [TestMethod]
    public void TryRemoveLoco_ExistingLoco_ReturnsTrue()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1234), "L1234"));

        var result = session.TryRemoveLoco("L1234");

        Assert.IsTrue(result);
        Assert.IsEmpty(session.Locos);
    }

    [TestMethod]
    public void TryRemoveLoco_NonExistentLoco_ReturnsFalse()
    {
        var session = new ThrottleSession();

        var result = session.TryRemoveLoco("L9999");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void GetLoco_ExistingLoco_ReturnsLoco()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1234), "L1234"));

        var result = session.GetLoco("L1234");

        Assert.IsNotNull(result);
        Assert.AreEqual(1234, result.Address.Number);
    }

    [TestMethod]
    public void GetLoco_NonExistentLoco_ReturnsNull()
    {
        var session = new ThrottleSession();

        var result = session.GetLoco("L9999");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetTargetLocos_Wildcard_ReturnsAllLocos()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1), "S1"));
        session.TryAddLoco(new LocoState(Address.From(2), "S2"));
        session.TryAddLoco(new LocoState(Address.From(3), "S3"));

        var result = session.GetTargetLocos("*").ToList();

        Assert.HasCount(3, result);
    }

    [TestMethod]
    public void GetTargetLocos_SpecificId_ReturnsSingleLoco()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1), "S1"));
        session.TryAddLoco(new LocoState(Address.From(2), "S2"));

        var result = session.GetTargetLocos("S1").ToList();

        Assert.HasCount(1, result);
        Assert.AreEqual(1, result[0].Address.Number);
    }

    [TestMethod]
    public void GetTargetLocos_NonExistentId_ReturnsEmpty()
    {
        var session = new ThrottleSession();
        session.TryAddLoco(new LocoState(Address.From(1), "S1"));

        var result = session.GetTargetLocos("S99").ToList();

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void TouchActivity_UpdatesLastActivity()
    {
        var session = new ThrottleSession();
        var before = session.LastActivity;

        // Small delay to ensure timestamp changes
        Thread.Sleep(10);
        session.TouchActivity();

        Assert.IsTrue(session.LastActivity > before);
    }
}
