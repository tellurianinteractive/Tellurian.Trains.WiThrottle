using Tellurian.Trains.WiThrottles.Protocol;

namespace Tellurian.Trains.WiThrottles.Tests.Protocol;

[TestClass]
public class WiThrottleParserTests
{
    [TestMethod]
    public void ParseThrottleName_ReturnsThrottleNameMessage()
    {
        var result = WiThrottleParser.Parse("NMyThrottle");

        Assert.IsInstanceOfType<WiThrottleMessage.ThrottleName>(result);
        Assert.AreEqual("MyThrottle", ((WiThrottleMessage.ThrottleName)result).Name);
    }

    [TestMethod]
    public void ParseHardwareId_ReturnsHardwareIdMessage()
    {
        var result = WiThrottleParser.Parse("HU1a2b3c4d5e6f");

        Assert.IsInstanceOfType<WiThrottleMessage.HardwareId>(result);
        Assert.AreEqual("1a2b3c4d5e6f", ((WiThrottleMessage.HardwareId)result).Id);
    }

    [TestMethod]
    public void ParseHeartbeatOptIn_ReturnsHeartbeatOptInMessage()
    {
        var result = WiThrottleParser.Parse("*+");

        Assert.IsInstanceOfType<WiThrottleMessage.HeartbeatOptIn>(result);
    }

    [TestMethod]
    public void ParseHeartbeat_ReturnsHeartbeatMessage()
    {
        var result = WiThrottleParser.Parse("*");

        Assert.IsInstanceOfType<WiThrottleMessage.Heartbeat>(result);
    }

    [TestMethod]
    public void ParseQuit_ReturnsQuitMessage()
    {
        var result = WiThrottleParser.Parse("Q");

        Assert.IsInstanceOfType<WiThrottleMessage.Quit>(result);
    }

    [TestMethod]
    public void ParseAcquireLoco_ReturnsAcquireLocoMessage()
    {
        var result = WiThrottleParser.Parse("MT+L1234<;>L1234");

        Assert.IsInstanceOfType<WiThrottleMessage.AcquireLoco>(result);
        Assert.AreEqual("L1234", ((WiThrottleMessage.AcquireLoco)result).LocoId);
    }

    [TestMethod]
    public void ParseReleaseLoco_ReturnsReleaseLocoMessage()
    {
        var result = WiThrottleParser.Parse("MT-L1234<;>r");

        Assert.IsInstanceOfType<WiThrottleMessage.ReleaseLoco>(result);
        Assert.AreEqual("L1234", ((WiThrottleMessage.ReleaseLoco)result).LocoId);
    }

    [TestMethod]
    public void ParseSetSpeed_ReturnsSetSpeedMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>V50");

        Assert.IsInstanceOfType<WiThrottleMessage.SetSpeed>(result);
        var msg = (WiThrottleMessage.SetSpeed)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.AreEqual((byte)50, msg.Speed);
    }

    [TestMethod]
    public void ParseSetDirection_Forward_ReturnsSetDirectionMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>R1");

        Assert.IsInstanceOfType<WiThrottleMessage.SetDirection>(result);
        var msg = (WiThrottleMessage.SetDirection)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.IsTrue(msg.Forward);
    }

    [TestMethod]
    public void ParseSetDirection_Backward_ReturnsSetDirectionMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>R0");

        Assert.IsInstanceOfType<WiThrottleMessage.SetDirection>(result);
        var msg = (WiThrottleMessage.SetDirection)result;
        Assert.IsFalse(msg.Forward);
    }

    [TestMethod]
    public void ParseEmergencyStop_ReturnsEmergencyStopMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>X");

        Assert.IsInstanceOfType<WiThrottleMessage.EmergencyStop>(result);
        Assert.AreEqual("L1234", ((WiThrottleMessage.EmergencyStop)result).Target);
    }

    [TestMethod]
    public void ParseFunctionToggle_On_ReturnsSetFunctionMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>F10");

        Assert.IsInstanceOfType<WiThrottleMessage.SetFunction>(result);
        var msg = (WiThrottleMessage.SetFunction)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.AreEqual(0, msg.FunctionNumber);
        Assert.IsTrue(msg.On);
    }

    [TestMethod]
    public void ParseFunctionToggle_Off_ReturnsSetFunctionMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>F05");

        Assert.IsInstanceOfType<WiThrottleMessage.SetFunction>(result);
        var msg = (WiThrottleMessage.SetFunction)result;
        Assert.AreEqual(5, msg.FunctionNumber);
        Assert.IsFalse(msg.On);
    }

    [TestMethod]
    public void ParseFunctionForce_ReturnsSetFunctionMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>f128");

        Assert.IsInstanceOfType<WiThrottleMessage.SetFunction>(result);
        var msg = (WiThrottleMessage.SetFunction)result;
        Assert.AreEqual(28, msg.FunctionNumber);
        Assert.IsTrue(msg.On);
    }

    [TestMethod]
    public void ParseFunctionMode_Momentary_ReturnsSetFunctionModeMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>m13");

        Assert.IsInstanceOfType<WiThrottleMessage.SetFunctionMode>(result);
        var msg = (WiThrottleMessage.SetFunctionMode)result;
        Assert.AreEqual(3, msg.FunctionNumber);
        Assert.IsTrue(msg.Momentary);
    }

    [TestMethod]
    public void ParseFunctionMode_Locking_ReturnsSetFunctionModeMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>m03");

        Assert.IsInstanceOfType<WiThrottleMessage.SetFunctionMode>(result);
        var msg = (WiThrottleMessage.SetFunctionMode)result;
        Assert.AreEqual(3, msg.FunctionNumber);
        Assert.IsFalse(msg.Momentary);
    }

    [TestMethod]
    public void ParseSpeedSteps_ReturnsSetSpeedStepsMessage()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>s128");

        Assert.IsInstanceOfType<WiThrottleMessage.SetSpeedSteps>(result);
        var msg = (WiThrottleMessage.SetSpeedSteps)result;
        Assert.AreEqual(128, msg.Steps);
    }

    [TestMethod]
    public void ParseWildcardTarget_ReturnsCorrectTarget()
    {
        var result = WiThrottleParser.Parse("MTA*<;>V100");

        Assert.IsInstanceOfType<WiThrottleMessage.SetSpeed>(result);
        Assert.AreEqual("*", ((WiThrottleMessage.SetSpeed)result).Target);
    }

    // Edge cases

    [TestMethod]
    public void ParseEmptyString_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseNull_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse(null!);

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseUnknownPrefix_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("ZZZZZ");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMalformedSpeed_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>Vabc");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMissingDelimiter_InAcquire_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MT+L1234");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMissingDelimiter_InAction_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MTAL1234V50");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseEmptyActionCommand_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseUnknownMultiThrottleAction_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MTZ<;>data");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseShortFunctionCommand_ReturnsUnknown()
    {
        var result = WiThrottleParser.Parse("MTAL1234<;>F1");

        Assert.IsInstanceOfType<WiThrottleMessage.Unknown>(result);
    }
}
