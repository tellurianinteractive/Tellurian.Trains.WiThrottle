# WiThrottle Server for WiFred

A .NET 10 WiThrottle protocol server that enables [WiFred](https://github.com/newHeiko/wiFred) throttles
to control model trains via LocoNet or Roco Z21 command stations.

**Important: This implementation *only* supports the part of the wiThrottle
protocol acually used by the **wiFRED**.**

## Features

- WiThrottle protocol v2.0 (WiFred-compatible subset)
- Up to 4 locos per WiFred, multiple concurrent WiFred connections
- mDNS service discovery (`_withrottle._tcp`)
- Heartbeat monitoring with automatic emergency stop
- Per-loco speed debouncing and global rate limiting
- LocoNet (serial, TCP, UDP multicast) and Z21 (UDP) command station adapters

## Prerequisites

- A LocoNet-compatible command station
- ROCO Z21 with a router, using UDP communication, **or**
- An USB-to-LocoNet device using serial communication, like the [RR-Cirkits LocoBuffer-NG](https://digira.se/webshop/ws-/products/locobuffer-ng), **or**
- A LoconetOverTcp server (e.g. JMRI, Rocrail, or LbServer) for TCP access, **or**
- A LocoNet UDP multicast gateway (e.g. loconetd or GCA101 LocoBuffer-UDP).

## Getting Started

### Running with Serial Communication

Edit `appsettings.json` to set your serial port, then run:

```json
{
  "CommandStation": {
    "Type": "Serial",
    "SerialPort": {
      "PortName": "COM3",
      "BaudRate": 57600
    }
  }
}
```

### Running with ROCO Z21

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

### Running with LocoNet over TCP

Connect to a LoconetOverTcp server (e.g. JMRI, Rocrail, or LbServer):

```json
{
  "CommandStation": {
    "Type": "LocoNetTcp",
    "LocoNetTcp": {
      "Hostname": "localhost",
      "Port": 1234
    }
  }
}
```

### Running with LocoNet over UDP Multicast

Connect via a UDP multicast gateway (e.g. loconetd or GCA101 LocoBuffer-UDP):

```json
{
  "CommandStation": {
    "Type": "LocoNetUdp",
    "LocoNetUdp": {
      "MulticastGroup": "224.0.0.1",
      "ListenPort": 1235,
      "SendAddress": "224.0.0.1",
      "SendPort": 1235,
      "ValidateChecksum": true
    }
  }
}
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
| `CommandStation` | `Type` | — | `Serial`, `Z21`, `LocoNetTcp`, or `LocoNetUdp` (required) |
| `CommandStation:SerialPort` | `PortName` | `COM3` | Serial port for LocoNet |
| `CommandStation:SerialPort` | `BaudRate` | `57600` | Baud rate for LocoNet |
| `CommandStation:Z21` | `Address` | `192.168.0.111` | Z21 IP address |
| `CommandStation:Z21` | `CommandPort` | `21105` | Z21 command UDP port |
| `CommandStation:Z21` | `FeedbackPort` | `21106` | Z21 feedback UDP port |
| `CommandStation:LocoNetTcp` | `Hostname` | `localhost` | LoconetOverTcp server hostname |
| `CommandStation:LocoNetTcp` | `Port` | `1234` | LoconetOverTcp server port |
| `CommandStation:LocoNetUdp` | `MulticastGroup` | `224.0.0.1` | UDP multicast group address |
| `CommandStation:LocoNetUdp` | `ListenPort` | `1235` | UDP multicast listen port |
| `CommandStation:LocoNetUdp` | `SendAddress` | `224.0.0.1` | UDP multicast send address |
| `CommandStation:LocoNetUdp` | `SendPort` | `1235` | UDP multicast send port |
| `CommandStation:LocoNetUdp` | `ValidateChecksum` | `true` | Validate LocoNet checksums on received datagrams |

Settings can also be overridden via environment variables or command-line arguments
using standard .NET configuration (e.g. `WiThrottle__Port=12345`).

## Known Limitations

- LocoNet adapter (Serial, TCP, and UDP) supports functions F0-F12 only; F13-F28 require Z21.
- WiFred functions F0-F16; functions beyond F16 are protocol-supported but not used by WiFred.
- The server implements only the WiFred-required subset of the WiThrottle protocol.
