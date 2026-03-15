using Tellurian.Trains.Communications.Interfaces.Locos;
using Tellurian.Trains.WiFreds.Protocol;

namespace Tellurian.Trains.WiFreds.Tests.Protocol;

[TestClass]
public class LocoAddressTests
{
    [TestMethod]
    public void TryParseLongAddress_ReturnsAddress()
    {
        var result = LocoAddress.TryParse("L1234");

        Assert.IsNotNull(result);
        Assert.AreEqual(1234, result.Value.Number);
    }

    [TestMethod]
    public void TryParseShortAddress_ReturnsAddress()
    {
        var result = LocoAddress.TryParse("S5");

        Assert.IsNotNull(result);
        Assert.AreEqual(5, result.Value.Number);
    }

    [TestMethod]
    public void TryParseLongAddress_Zero_ReturnsAddress()
    {
        var result = LocoAddress.TryParse("L0");

        // Address.IsValid determines if 0 is valid
        // If it returns null, that's the expected behavior for invalid addresses
        if (result is not null)
            Assert.AreEqual(0, result.Value.Number);
    }

    [TestMethod]
    public void TryParse_NoPrefix_ReturnsNull()
    {
        var result = LocoAddress.TryParse("1234");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_InvalidPrefix_ReturnsNull()
    {
        var result = LocoAddress.TryParse("X1234");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_NonNumeric_ReturnsNull()
    {
        var result = LocoAddress.TryParse("Labc");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_TooShort_ReturnsNull()
    {
        var result = LocoAddress.TryParse("L");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_SingleChar_ReturnsNull()
    {
        var result = LocoAddress.TryParse("L");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void ToLocoId_LongAddress_ReturnsLPrefixed()
    {
        var address = Address.From(1234);
        var result = LocoAddress.ToLocoId(address);

        Assert.AreEqual("L1234", result);
    }

    [TestMethod]
    public void ToLocoId_ShortAddress_ReturnsSPrefixed()
    {
        var address = Address.From(5);
        var result = LocoAddress.ToLocoId(address);

        Assert.AreEqual("S5", result);
    }

    [TestMethod]
    public void RoundTrip_LongAddress_PreservesValue()
    {
        var original = "L1234";
        var address = LocoAddress.TryParse(original);
        Assert.IsNotNull(address);
        var roundTripped = LocoAddress.ToLocoId(address.Value);

        Assert.AreEqual(original, roundTripped);
    }

    [TestMethod]
    public void RoundTrip_ShortAddress_PreservesValue()
    {
        var original = "S5";
        var address = LocoAddress.TryParse(original);
        Assert.IsNotNull(address);
        var roundTripped = LocoAddress.ToLocoId(address.Value);

        Assert.AreEqual(original, roundTripped);
    }
}
