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
        XmlValue("throttleName");

    public string? FirmwareVersion =>
        XmlValue("firmwareRevision");

    public string? BatteryVoltage =>
        XmlValue("batteryVoltage");

    /// <summary>
    /// Battery level as percentage (0–100), based on a typical single-cell LiPo discharge curve.
    /// Returns null if battery voltage is not available.
    /// </summary>
    public int? BatteryPercent
    {
        get
        {
            var raw = BatteryVoltage;
            if (raw is null || !int.TryParse(raw, out var mV)) return null;
            return LiPoPercentage(mV);
        }
    }

    /// <summary>
    /// Typical single-cell LiPo discharge curve: mV → percentage.
    /// Uses linear interpolation between reference points.
    /// </summary>
    internal static int LiPoPercentage(int milliVolts)
    {
        ReadOnlySpan<(int mV, int pct)> curve =
        [
            (4200, 100),
            (4150,  95),
            (4110,  90),
            (4080,  85),
            (4020,  80),
            (3980,  75),
            (3950,  70),
            (3910,  65),
            (3870,  60),
            (3830,  55),
            (3790,  50),
            (3750,  45),
            (3710,  40),
            (3670,  35),
            (3630,  30),
            (3590,  25),
            (3570,  20),
            (3530,  15),
            (3490,  10),
            (3450,   5),
            (3300,   0),
        ];

        if (milliVolts >= curve[0].mV) return 100;
        if (milliVolts <= curve[^1].mV) return 0;

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var (highMv, highPct) = curve[i];
            var (lowMv, lowPct) = curve[i + 1];
            if (milliVolts >= lowMv)
                return lowPct + (highPct - lowPct) * (milliVolts - lowMv) / (highMv - lowMv);
        }
        return 0;
    }

    public bool IsBatteryLow =>
        XmlValue("batteryLow") == "1";

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

    public string? ConnectedSsid =>
        Configuration?.Root?.Element("WiFi")?.Element("SSID")?.Attribute("value")?.Value;

    public IReadOnlyList<WiFredNetwork> ConfiguredNetworks
    {
        get
        {
            var networks = Configuration?.Root?.Element("NETWORKS");
            if (networks is null) return [];

            return networks.Elements("NETWORK").Select(n =>
            {
                var ssid = n.Element("SSID")?.Attribute("value")?.Value ?? "";
                var enabled = n.Element("Enabled")?.Attribute("value")?.Value == "1";
                return new WiFredNetwork(ssid, enabled);
            }).ToList();
        }
    }

    public IReadOnlyList<WiFredNetwork> ExtraEnabledNetworks
    {
        get
        {
            var connected = ConnectedSsid;
            if (connected is null) return [];
            return ConfiguredNetworks
                .Where(n => n.Enabled && !string.Equals(n.Ssid, connected, StringComparison.Ordinal))
                .ToList();
        }
    }

    private static int ParseAddress(XElement element)
    {
        var value = element.Element("DCCadress")?.Attribute("value")?.Value
            ?? element.Element("address")?.Value;
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
public sealed record WiFredNetwork(string Ssid, bool Enabled);
