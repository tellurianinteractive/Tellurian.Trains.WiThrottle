using Tellurian.Trains.WiFreds.Protocol;

namespace Tellurian.Trains.WiFreds.Tests.Protocol;

[TestClass]
public class WiFredParserTests
{
    [TestMethod]
    public void ParseThrottleName_ReturnsThrottleNameMessage()
    {
        var result = WiFredParser.Parse("NMyThrottle");

        Assert.IsInstanceOfType<WiFredMessage.ThrottleName>(result);
        Assert.AreEqual("MyThrottle", ((WiFredMessage.ThrottleName)result).Name);
    }

    [TestMethod]
    public void ParseHardwareId_ReturnsHardwareIdMessage()
    {
        var result = WiFredParser.Parse("HU1a2b3c4d5e6f");

        Assert.IsInstanceOfType<WiFredMessage.HardwareId>(result);
        Assert.AreEqual("1a2b3c4d5e6f", ((WiFredMessage.HardwareId)result).Id);
    }

    [TestMethod]
    public void ParseHeartbeatOptIn_ReturnsHeartbeatOptInMessage()
    {
        var result = WiFredParser.Parse("*+");

        Assert.IsInstanceOfType<WiFredMessage.HeartbeatOptIn>(result);
    }

    [TestMethod]
    public void ParseHeartbeat_ReturnsHeartbeatMessage()
    {
        var result = WiFredParser.Parse("*");

        Assert.IsInstanceOfType<WiFredMessage.Heartbeat>(result);
    }

    [TestMethod]
    public void ParseQuit_ReturnsQuitMessage()
    {
        var result = WiFredParser.Parse("Q");

        Assert.IsInstanceOfType<WiFredMessage.Quit>(result);
    }

    [TestMethod]
    public void ParseAcquireLoco_ReturnsAcquireLocoMessage()
    {
        var result = WiFredParser.Parse("MT+L1234<;>L1234");

        Assert.IsInstanceOfType<WiFredMessage.AcquireLoco>(result);
        Assert.AreEqual("L1234", ((WiFredMessage.AcquireLoco)result).LocoId);
    }

    [TestMethod]
    public void ParseReleaseLoco_ReturnsReleaseLocoMessage()
    {
        var result = WiFredParser.Parse("MT-L1234<;>r");

        Assert.IsInstanceOfType<WiFredMessage.ReleaseLoco>(result);
        Assert.AreEqual("L1234", ((WiFredMessage.ReleaseLoco)result).LocoId);
    }

    [TestMethod]
    public void ParseSetSpeed_ReturnsSetSpeedMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>V50");

        Assert.IsInstanceOfType<WiFredMessage.SetSpeed>(result);
        var msg = (WiFredMessage.SetSpeed)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.AreEqual((byte)50, msg.Speed);
    }

    [TestMethod]
    public void ParseSetDirection_Forward_ReturnsSetDirectionMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>R1");

        Assert.IsInstanceOfType<WiFredMessage.SetDirection>(result);
        var msg = (WiFredMessage.SetDirection)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.IsTrue(msg.Forward);
    }

    [TestMethod]
    public void ParseSetDirection_Backward_ReturnsSetDirectionMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>R0");

        Assert.IsInstanceOfType<WiFredMessage.SetDirection>(result);
        var msg = (WiFredMessage.SetDirection)result;
        Assert.IsFalse(msg.Forward);
    }

    [TestMethod]
    public void ParseEmergencyStop_ReturnsEmergencyStopMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>X");

        Assert.IsInstanceOfType<WiFredMessage.EmergencyStop>(result);
        Assert.AreEqual("L1234", ((WiFredMessage.EmergencyStop)result).Target);
    }

    [TestMethod]
    public void ParseFunctionToggle_On_ReturnsSetFunctionMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>F10");

        Assert.IsInstanceOfType<WiFredMessage.SetFunction>(result);
        var msg = (WiFredMessage.SetFunction)result;
        Assert.AreEqual("L1234", msg.Target);
        Assert.AreEqual(0, msg.FunctionNumber);
        Assert.IsTrue(msg.On);
        Assert.IsFalse(msg.IsForce);
    }

    [TestMethod]
    public void ParseFunctionToggle_Off_ReturnsSetFunctionMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>F05");

        Assert.IsInstanceOfType<WiFredMessage.SetFunction>(result);
        var msg = (WiFredMessage.SetFunction)result;
        Assert.AreEqual(5, msg.FunctionNumber);
        Assert.IsFalse(msg.On);
        Assert.IsFalse(msg.IsForce);
    }

    [TestMethod]
    public void ParseFunctionForce_ReturnsSetFunctionMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>f128");

        Assert.IsInstanceOfType<WiFredMessage.SetFunction>(result);
        var msg = (WiFredMessage.SetFunction)result;
        Assert.AreEqual(28, msg.FunctionNumber);
        Assert.IsTrue(msg.On);
        Assert.IsTrue(msg.IsForce);
    }

    [TestMethod]
    public void ParseFunctionMode_Momentary_ReturnsSetFunctionModeMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>m13");

        Assert.IsInstanceOfType<WiFredMessage.SetFunctionMode>(result);
        var msg = (WiFredMessage.SetFunctionMode)result;
        Assert.AreEqual(3, msg.FunctionNumber);
        Assert.IsTrue(msg.Momentary);
    }

    [TestMethod]
    public void ParseFunctionMode_Locking_ReturnsSetFunctionModeMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>m03");

        Assert.IsInstanceOfType<WiFredMessage.SetFunctionMode>(result);
        var msg = (WiFredMessage.SetFunctionMode)result;
        Assert.AreEqual(3, msg.FunctionNumber);
        Assert.IsFalse(msg.Momentary);
    }

    [TestMethod]
    public void ParseSpeedSteps_ReturnsSetSpeedStepsMessage()
    {
        var result = WiFredParser.Parse("MTAL1234<;>s128");

        Assert.IsInstanceOfType<WiFredMessage.SetSpeedSteps>(result);
        var msg = (WiFredMessage.SetSpeedSteps)result;
        Assert.AreEqual(128, msg.Steps);
    }

    [TestMethod]
    public void ParseWildcardTarget_ReturnsCorrectTarget()
    {
        var result = WiFredParser.Parse("MTA*<;>V100");

        Assert.IsInstanceOfType<WiFredMessage.SetSpeed>(result);
        Assert.AreEqual("*", ((WiFredMessage.SetSpeed)result).Target);
    }

    // Edge cases

    [TestMethod]
    public void ParseEmptyString_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseNull_ReturnsUnknown()
    {
        var result = WiFredParser.Parse(null!);

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseUnknownPrefix_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("ZZZZZ");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMalformedSpeed_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MTAL1234<;>Vabc");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMissingDelimiter_InAcquire_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MT+L1234");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseMissingDelimiter_InAction_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MTAL1234V50");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseEmptyActionCommand_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MTAL1234<;>");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseUnknownMultiThrottleAction_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MTZ<;>data");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }

    [TestMethod]
    public void ParseShortFunctionCommand_ReturnsUnknown()
    {
        var result = WiFredParser.Parse("MTAL1234<;>F1");

        Assert.IsInstanceOfType<WiFredMessage.Unknown>(result);
    }
}
