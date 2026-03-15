namespace Tellurian.Trains.WiFreds.Protocol;

/// <summary>
/// Parses a single WiFred protocol line into a <see cref="WiFredMessage"/>.
/// </summary>
public static class WiFredParser
{
    private const string ActionDelimiter = "<;>";

    public static WiFredMessage Parse(string line)
    {
        if (string.IsNullOrEmpty(line))
            return new WiFredMessage.Unknown(line ?? "");

        // Heartbeat opt-in: *+
        if (line == "*+")
            return new WiFredMessage.HeartbeatOptIn();

        // Heartbeat keepalive: *
        if (line == "*")
            return new WiFredMessage.Heartbeat();

        // Quit: Q
        if (line == "Q")
            return new WiFredMessage.Quit();

        // Throttle name: N{name}
        if (line.StartsWith('N'))
            return new WiFredMessage.ThrottleName(line[1..]);

        // Hardware ID: HU{macHex}
        if (line.StartsWith("HU", StringComparison.Ordinal))
            return new WiFredMessage.HardwareId(line[2..]);

        // Multi-throttle commands: MT{action}{rest}<;>{data}
        if (line.StartsWith("MT", StringComparison.Ordinal) && line.Length > 2)
            return ParseMultiThrottle(line);

        return new WiFredMessage.Unknown(line);
    }

    private static WiFredMessage ParseMultiThrottle(string line)
    {
        var action = line[2];

        return action switch
        {
            '+' => ParseAcquire(line),
            '-' => ParseRelease(line),
            'A' => ParseAction(line),
            _ => new WiFredMessage.Unknown(line)
        };
    }

    private static WiFredMessage ParseAcquire(string line)
    {
        // MT+{locoId}<;>{locoId}
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiFredMessage.Unknown(line);

        var locoId = line[3..delimiterIndex];
        return new WiFredMessage.AcquireLoco(locoId);
    }

    private static WiFredMessage ParseRelease(string line)
    {
        // MT-{locoId}<;>r
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiFredMessage.Unknown(line);

        var locoId = line[3..delimiterIndex];
        return new WiFredMessage.ReleaseLoco(locoId);
    }

    private static WiFredMessage ParseAction(string line)
    {
        // MTA{target}<;>{command}
        var delimiterIndex = line.IndexOf(ActionDelimiter, StringComparison.Ordinal);
        if (delimiterIndex < 0) return new WiFredMessage.Unknown(line);

        var target = line[3..delimiterIndex];
        var command = line[(delimiterIndex + ActionDelimiter.Length)..];

        if (command.Length == 0) return new WiFredMessage.Unknown(line);

        return command[0] switch
        {
            'V' => ParseSpeed(target, command),
            'R' => ParseDirection(target, command),
            'X' => new WiFredMessage.EmergencyStop(target),
            'F' => ParseFunction(target, command, isForce: false),
            'f' => ParseFunction(target, command, isForce: true),
            'm' => ParseFunctionMode(target, command),
            's' => ParseSpeedSteps(target, command),
            _ => new WiFredMessage.Unknown(line)
        };
    }

    private static WiFredMessage ParseSpeed(string target, string command)
    {
        // V{speed}
        if (byte.TryParse(command.AsSpan(1), out var speed))
            return new WiFredMessage.SetSpeed(target, speed);
        return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiFredMessage ParseDirection(string target, string command)
    {
        // R0 or R1
        if (command.Length >= 2 && (command[1] == '0' || command[1] == '1'))
            return new WiFredMessage.SetDirection(target, command[1] == '1');
        return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiFredMessage ParseFunction(string target, string command, bool isForce)
    {
        // F{0or1}{funcNum} or f{0or1}{funcNum}
        if (command.Length < 3) return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
        var on = command[1] == '1';
        if (int.TryParse(command.AsSpan(2), out var funcNum))
            return new WiFredMessage.SetFunction(target, funcNum, on, isForce);
        return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiFredMessage ParseFunctionMode(string target, string command)
    {
        // m{0or1}{funcNum}
        if (command.Length < 3) return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
        var momentary = command[1] == '1';
        if (int.TryParse(command.AsSpan(2), out var funcNum))
            return new WiFredMessage.SetFunctionMode(target, funcNum, momentary);
        return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
    }

    private static WiFredMessage ParseSpeedSteps(string target, string command)
    {
        // s{mode}
        if (int.TryParse(command.AsSpan(1), out var steps))
            return new WiFredMessage.SetSpeedSteps(target, steps);
        return new WiFredMessage.Unknown($"MTA{target}<;>{command}");
    }
}
