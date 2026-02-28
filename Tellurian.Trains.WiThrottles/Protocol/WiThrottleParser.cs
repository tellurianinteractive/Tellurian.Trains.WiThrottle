namespace Tellurian.Trains.WiThrottles.Protocol;

/// <summary>
/// Parses a single WiThrottle protocol line into a <see cref="WiThrottleMessage"/>.
/// </summary>
public static class WiThrottleParser
{
    private const string ActionDelimiter = "<;>";

    public static WiThrottleMessage Parse(string line)
    {
        if (string.IsNullOrEmpty(line))
            return new WiThrottleMessage.Unknown(line ?? "");

        // Heartbeat opt-in: *+
        if (line == "*+")
            return new WiThrottleMessage.HeartbeatOptIn();

        // Heartbeat keepalive: *
        if (line == "*")
            return new WiThrottleMessage.Heartbeat();

        // Quit: Q
        if (line == "Q")
            return new WiThrottleMessage.Quit();

        // Throttle name: N{name}
        if (line.StartsWith('N'))
            return new WiThrottleMessage.ThrottleName(line[1..]);

        // Hardware ID: HU{macHex}
        if (line.StartsWith("HU", StringComparison.Ordinal))
            return new WiThrottleMessage.HardwareId(line[2..]);

        // Multi-throttle commands: MT{action}{rest}<;>{data}
        if (line.StartsWith("MT", StringComparison.Ordinal) && line.Length > 2)
            return ParseMultiThrottle(line);

        return new WiThrottleMessage.Unknown(line);
    }

    private static WiThrottleMessage ParseMultiThrottle(string line)
    {
        var action = line[2];

        return action switch
        {
            '+' => ParseAcquire(line),
            '-' => ParseRelease(line),
            'A' => ParseAction(line),
            _ => new WiThrottleMessage.Unknown(line)
        };
    }

    private static WiThrottleMessage ParseAcquire(string line)
    {
        // MT+{locoId}<;>{locoId}
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiThrottleMessage.Unknown(line);

        var locoId = line[3..delimiterIndex];
        return new WiThrottleMessage.AcquireLoco(locoId);
    }

    private static WiThrottleMessage ParseRelease(string line)
    {
        // MT-{locoId}<;>r
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiThrottleMessage.Unknown(line);

        var locoId = line[3..delimiterIndex];
        return new WiThrottleMessage.ReleaseLoco(locoId);
    }

    private static WiThrottleMessage ParseAction(string line)
    {
        // MTA{target}<;>{command}
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiThrottleMessage.Unknown(line);

        var target = line[3..delimiterIndex];
        var command = line[(delimiterIndex + ActionDelimiter.Length)..];

        if (command.Length == 0) return new WiThrottleMessage.Unknown(line);

        return command[0] switch
        {
            'V' => ParseSpeed(target, command),
            'R' => ParseDirection(target, command),
            'X' => new WiThrottleMessage.EmergencyStop(target),
            'F' => ParseFunction(target, command, isForce: false),
            'f' => ParseFunction(target, command, isForce: true),
            'm' => ParseFunctionMode(target, command),
            's' => ParseSpeedSteps(target, command),
            _ => new WiThrottleMessage.Unknown(line)
        };
    }

    private static WiThrottleMessage ParseSpeed(string target, string command)
    {
        // V{speed}
        if (byte.TryParse(command.AsSpan(1), out var speed))
            return new WiThrottleMessage.SetSpeed(target, speed);
        return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiThrottleMessage ParseDirection(string target, string command)
    {
        // R0 or R1
        if (command.Length >= 2 && (command[1] == '0' || command[1] == '1'))
            return new WiThrottleMessage.SetDirection(target, command[1] == '1');
        return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiThrottleMessage ParseFunction(string target, string command, bool isForce)
    {
        // F{0or1}{funcNum} or f{0or1}{funcNum}
        if (command.Length < 3) return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
        var on = command[1] == '1';
        if (int.TryParse(command.AsSpan(2), out var funcNum))
            return new WiThrottleMessage.SetFunction(target, funcNum, on);
        return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiThrottleMessage ParseFunctionMode(string target, string command)
    {
        // m{0or1}{funcNum}
        if (command.Length < 3) return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
        var momentary = command[1] == '1';
        if (int.TryParse(command.AsSpan(2), out var funcNum))
            return new WiThrottleMessage.SetFunctionMode(target, funcNum, momentary);
        return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiThrottleMessage ParseSpeedSteps(string target, string command)
    {
        // s{mode}
        if (int.TryParse(command.AsSpan(1), out var steps))
            return new WiThrottleMessage.SetSpeedSteps(target, steps);
        return new WiThrottleMessage.Unknown($"MTA{target}<;>{command}");
    }
}
