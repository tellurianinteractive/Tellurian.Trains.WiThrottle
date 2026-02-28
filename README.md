# WiThrottle Server for WiFred

A .NET 10 WiThrottle protocol server that enables [WiFred](https://github.com/newHeiko/wiFred) throttles
to control model trains via LocoNet or Roco Z21 command stations.

## Features

- WiThrottle protocol v2.0 (WiFred-compatible subset)
- Up to 4 locos per WiFred, multiple concurrent WiFred connections
- mDNS service discovery (`_withrottle._tcp`)
- Heartbeat monitoring with automatic emergency stop
- Per-loco speed debouncing and global rate limiting
- LocoNet (serial) and Z21 (UDP) command station adapters

## Prerequisites

- .NET 10 SDK
- A LocoNet-compatible command station with serial interface, **or** a Roco Z21

## Getting Started

### Development Mode (no hardware)

In development mode the server uses a logging mock instead of a real command station.
All loco commands are logged to the console.

```
dotnet run --project Tellurian.Trains.WiThrottles --environment Development
```

### Production Mode with LocoNet

Edit `appsettings.json` to set your serial port, then run:

```json
{
  "CommandStation": {
    "Type": "LocoNet",
    "SerialPort": {
      "PortName": "COM3",
      "BaudRate": 57600
    }
  }
}
```

```
dotnet run --project Tellurian.Trains.WiThrottles
```

### Production Mode with Z21

Set the command station type to `Z21` and configure the Z21 network address:

```json
{
  "CommandStation": {
    "Type": "Z21",
    "Z21": {
      "Address": "192.168.0.111",
      "CommandPort": 21105,
      "FeedbackPort": 21106
    }
  }
}
```

```
dotnet run --project Tellurian.Trains.WiThrottles
```

## Configuration

All settings are in `appsettings.json`:

| Section | Setting | Default | Description |
|---------|---------|---------|-------------|
| `WiThrottle` | `Port` | `12090` | TCP port for WiFred connections |
| `WiThrottle` | `HeartbeatTimeoutSeconds` | `10` | Seconds before inactive client triggers e-stop |
| `WiThrottle` | `ServiceName` | `WiThrottle Server` | mDNS service name |
| `Throttling` | `SpeedTimeThresholdMs` | `150` | Min ms between forwarded speed commands per loco |
| `Throttling` | `SpeedStepThreshold` | `2` | Min speed step change to bypass time threshold |
| `Throttling` | `GlobalMessageRatePerSecond` | `20` | Max command station messages/sec across all clients |
| `CommandStation` | `Type` | `LocoNet` | `LocoNet` or `Z21` |
| `CommandStation:SerialPort` | `PortName` | `COM3` | Serial port for LocoNet |
| `CommandStation:SerialPort` | `BaudRate` | `57600` | Baud rate for LocoNet |
| `CommandStation:Z21` | `Address` | `192.168.0.111` | Z21 IP address |
| `CommandStation:Z21` | `CommandPort` | `21105` | Z21 command UDP port |
| `CommandStation:Z21` | `FeedbackPort` | `21106` | Z21 feedback UDP port |

Settings can also be overridden via environment variables or command-line arguments
using standard .NET configuration (e.g. `WiThrottle__Port=12345`).

## Project Structure

```
Tellurian.Trains.WiThrottles/
  Program.cs                              -- Host setup and DI configuration
  Configuration/
    WiThrottleSettings.cs                 -- Server settings (port, heartbeat, service name)
    ThrottlingSettings.cs                 -- Speed debounce and rate limit settings
    CommandStationSettings.cs             -- LocoNet / Z21 connection settings
  Protocol/
    WiThrottleMessage.cs                  -- Discriminated union of all protocol messages
    WiThrottleParser.cs                   -- Line parser -> WiThrottleMessage
    LocoAddress.cs                        -- L1234/S5 <-> Address conversion
  Sessions/
    ThrottleSession.cs                    -- Per-client session state (up to 4 locos)
    SessionHandler.cs                     -- Message handler -> ILoco calls + responses
    LocoState.cs                          -- Per-loco mutable state (speed, direction, functions)
  Throttling/
    ThrottledLocoController.cs            -- ILoco wrapper with speed debounce + rate limit
    SpeedThrottler.cs                     -- Per-loco speed debouncing with trailing edge
    GlobalRateLimiter.cs                  -- Token bucket rate limiter
  Server/
    WiThrottleTcpServer.cs               -- TCP listener, client sessions, heartbeat monitor
    MdnsAdvertiser.cs                     -- mDNS service advertisement
    CommandStationInitializer.cs          -- Starts the command station adapter
  Development/
    LoggingLocoController.cs              -- Mock ILoco for development mode

Tellurian.Trains.WiThrottles.Tests/
  Protocol/
    WiThrottleParserTests.cs              -- Parser tests for all message types
    LocoAddressTests.cs                   -- Address conversion and round-trip tests
  Sessions/
    ThrottleSessionTests.cs               -- Session state management tests
    SessionHandlerTests.cs                -- Message handling -> ILoco call verification
  Throttling/
    SpeedThrottlerTests.cs                -- Debounce threshold and trailing edge tests
    GlobalRateLimiterTests.cs             -- Token bucket behavior tests
  Integration/
    SimulatedWiFredTests.cs               -- End-to-end TCP tests simulating WiFred
  Helpers/
    RecordingLocoController.cs            -- ILoco test double that records all calls
```

## Running Tests

```
dotnet run --project Tellurian.Trains.WiThrottles.Tests
```

## Known Limitations

- LocoNet adapter supports functions F0-F12 only; F13-F28 require Z21.
- WiFred functions F0-F16; functions beyond F16 are protocol-supported but not used by WiFred.
- The server implements only the WiFred-required subset of the WiThrottle protocol.
