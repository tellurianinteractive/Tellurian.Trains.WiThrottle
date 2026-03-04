using System.Net;
using System.Xml.Linq;

namespace Tellurian.Trains.WiThrottles.Server;

public sealed class WiFredDevice(IPAddress address)
{
    public IPAddress Address { get; } = address;
    public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
    public XDocument? Configuration { get; set; }

    public string? Name =>
        Configuration?.Root?.Element("throttleName")?.Value;

    public IReadOnlyList<int> LocoAddresses
    {
        get
        {
            var locos = Configuration?.Root?.Element("LOCOS");
            if (locos is null) return [];

            return locos.Elements()
                .Select(e => e.Element("address")?.Value)
                .Where(v => v is not null && int.TryParse(v, out var a) && a > 0)
                .Select(v => int.Parse(v!))
                .ToList();
        }
    }
}

public sealed record LocoAddressConflict(int Address, IReadOnlyList<WiFredDevice> Devices);
