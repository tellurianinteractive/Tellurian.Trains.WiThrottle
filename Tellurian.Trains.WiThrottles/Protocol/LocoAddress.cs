using Tellurian.Trains.Communications.Interfaces.Locos;

namespace Tellurian.Trains.WiThrottles.Protocol;

/// <summary>
/// Converts between WiFred loco ID strings (e.g. "L1234", "S5") and <see cref="Address"/>.
/// </summary>
public static class LocoAddress
{
    /// <summary>
    /// Parses a WiFred loco ID string to an <see cref="Address"/>.
    /// </summary>
    /// <param name="locoId">A string like "L1234" (long) or "S5" (short).</param>
    /// <returns>The parsed address, or null if the format is invalid.</returns>
    public static Address? TryParse(string locoId)
    {
        if (locoId.Length < 2) return null;
        var prefix = locoId[0];
        if (prefix is not ('L' or 'S')) return null;
        if (!int.TryParse(locoId.AsSpan(1), out var number)) return null;
        if (!Address.IsValid((short)number)) return null;
        return Address.From(number);
    }

    /// <summary>
    /// Converts an <see cref="Address"/> to a WiFred loco ID string.
    /// </summary>
    public static string ToLocoId(Address address) =>
        address.IsLong ? $"L{address.Number}" : $"S{address.Number}";
}
