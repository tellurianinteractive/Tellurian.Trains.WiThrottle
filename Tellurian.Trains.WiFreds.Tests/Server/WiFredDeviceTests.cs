using System.Net;
using System.Xml.Linq;
using Tellurian.Trains.WiFreds.Server;

namespace Tellurian.Trains.WiFreds.Tests.Server;

[TestClass]
public class WiFredDeviceTests
{
    private static XDocument CreateConfigXml(string name, params int[] addresses)
    {
        var locos = new XElement("LOCOS",
            addresses.Select((a, i) => new XElement($"loco{i}",
                new XElement("address", a.ToString()))));

        return new XDocument(
            new XElement("config",
                new XElement("throttleName", name),
                locos));
    }

    [TestMethod]
    public void Name_ParsedFromXml()
    {
        var device = new WiFredDevice(IPAddress.Loopback)
        {
            Configuration = CreateConfigXml("MyThrottle", 3)
        };

        Assert.AreEqual("MyThrottle", device.Name);
    }

    [TestMethod]
    public void Name_NullWhenNoConfiguration()
    {
        var device = new WiFredDevice(IPAddress.Loopback);
        Assert.IsNull(device.Name);
    }

    [TestMethod]
    public void LocoAddresses_ParsedFromXml()
    {
        var device = new WiFredDevice(IPAddress.Loopback)
        {
            Configuration = CreateConfigXml("Test", 3, 42, 100)
        };

        CollectionAssert.AreEqual(new[] { 3, 42, 100 }, device.LocoAddresses.ToList());
    }

    [TestMethod]
    public void LocoAddresses_SkipsZeroAddresses()
    {
        var device = new WiFredDevice(IPAddress.Loopback)
        {
            Configuration = CreateConfigXml("Test", 3, 0, 42)
        };

        CollectionAssert.AreEqual(new[] { 3, 42 }, device.LocoAddresses.ToList());
    }

    [TestMethod]
    public void LocoAddresses_EmptyWhenNoConfiguration()
    {
        var device = new WiFredDevice(IPAddress.Loopback);
        Assert.IsEmpty(device.LocoAddresses);
    }

    [TestMethod]
    public void LocoAddresses_EmptyWhenNoLocosElement()
    {
        var device = new WiFredDevice(IPAddress.Loopback)
        {
            Configuration = new XDocument(new XElement("config",
                new XElement("throttleName", "Test")))
        };

        Assert.IsEmpty(device.LocoAddresses);
    }
}

[TestClass]
public class LocoAddressConflictTests
{
    private static WiFredDevice CreateDevice(string ip, string name, params int[] addresses)
    {
        var locos = new XElement("LOCOS",
            addresses.Select((a, i) => new XElement($"loco{i}",
                new XElement("address", a.ToString()))));

        return new WiFredDevice(IPAddress.Parse(ip))
        {
            Configuration = new XDocument(
                new XElement("config",
                    new XElement("throttleName", name),
                    locos))
        };
    }

    [TestMethod]
    public void DetectsConflict_WhenTwoDevicesShareAddress()
    {
        var device1 = CreateDevice("10.0.0.1", "Throttle1", 3, 42);
        var device2 = CreateDevice("10.0.0.2", "Throttle2", 42, 100);

        var devices = new[] { device1, device2 };
        var conflicts = FindConflicts(devices);

        Assert.HasCount(1, conflicts);
        Assert.AreEqual(42, conflicts[0].Address);
        Assert.HasCount(2, conflicts[0].Devices);
    }

    [TestMethod]
    public void NoConflicts_WhenAllAddressesUnique()
    {
        var device1 = CreateDevice("10.0.0.1", "Throttle1", 3, 42);
        var device2 = CreateDevice("10.0.0.2", "Throttle2", 100, 200);

        var conflicts = FindConflicts([device1, device2]);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void NoConflicts_WhenSingleDevice()
    {
        var device = CreateDevice("10.0.0.1", "Throttle1", 3, 42);
        var conflicts = FindConflicts([device]);

        Assert.IsEmpty(conflicts);
    }

    [TestMethod]
    public void DetectsMultipleConflicts()
    {
        var device1 = CreateDevice("10.0.0.1", "Throttle1", 3, 42);
        var device2 = CreateDevice("10.0.0.2", "Throttle2", 3, 42);

        var conflicts = FindConflicts([device1, device2]);

        Assert.HasCount(2, conflicts);
        Assert.IsTrue(conflicts.Any(c => c.Address == 3));
        Assert.IsTrue(conflicts.Any(c => c.Address == 42));
    }

    private static IReadOnlyList<LocoAddressConflict> FindConflicts(IReadOnlyList<WiFredDevice> devices)
    {
        return devices
            .SelectMany(d => d.LocoAddresses.Select(a => (Address: a, Device: d)))
            .GroupBy(x => x.Address)
            .Where(g => g.Count() > 1)
            .Select(g => new LocoAddressConflict(g.Key, g.Select(x => x.Device).ToList()))
            .ToList();
    }
}
