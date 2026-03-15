using System.Net;
using System.Xml.Linq;

namespace Tellurian.Trains.WiFreds.Server;

public sealed class WiFredDevice(IPAddress address)
{
    public const int LocoSlotCount = 4;

    public IPAddress Address { get; } = address;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public XDocument? Configuration { get; set; }

    public string? Name =>
        Configuration?.Root?.Element("throttleName")?.Value;

    public string? FirmwareVersion =>
        XmlValue("firmwareRevision");

    public string? BatteryVoltage =>
        XmlValue("batteryVoltage");

    public IReadOnlyList<LocoSlot> LocoSlots
    {
        get
        {
            var locos = Configuration?.Root?.Element("LOCOS");
            if (locos is null)
                return Enumerable.Range(1, LocoSlotCount).Select(i => new LocoSlot(i, 0)).ToList();

            var elements = locos.Elements().ToList();
            return Enumerable.Range(1, LocoSlotCount).Select(i =>
            {
                var address = i <= elements.Count
                    ? ParseAddress(elements[i - 1])
                    : 0;
                return new LocoSlot(i, address);
            }).ToList();
        }
    }

    public IReadOnlyList<int> LocoAddresses =>
        LocoSlots.Where(s => s.Address > 0).Select(s => s.Address).ToList();

    private static int ParseAddress(XElement element)
    {
        var value = element.Element("address")?.Value;
        return value is not null && int.TryParse(value, out var a) ? a : 0;
    }

    private string? XmlValue(string elementName)
    {
        var element = Configuration?.Root?.Element(elementName);
        if (element is null) return null;
        var text = element.Value;
        if (!string.IsNullOrEmpty(text)) return text;
        return element.Attribute("value")?.Value;
    }
}

public sealed record LocoSlot(int Slot, int Address);
public sealed record LocoAddressConflict(int Address, IReadOnlyList<WiFredDevice> Devices);
