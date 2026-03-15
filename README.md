# WiFred Server

A .NET 10 WiThrottle protocol server that enables [WiFred](https://github.com/newHeiko/wiFred) throttles
to control model trains via any command station that has LocoNet support or a network API (e.g. Roco Z21).

**Important: This implementation *only* supports the part of the wiThrottle
protocol acually used by the **wiFRED**.**

## References
- [wiFred source code](https://github.com/newHeiko/wiFred),
- [JMRI wiThrottle protocol](https://www.jmri.org/help/en/package/jmri/jmrit/withrottle/Protocol.shtml),
- [wiFRED user manual](https://newheiko.github.io/wiFred/documentation/docu_en.html)

## Features

- WiThrottle protocol v2.0 (WiFred-compatible subset)
- Up to 4 locos per WiFred, multiple concurrent WiFred connections
- mDNS service discovery (`_withrottle._tcp`)
- Heartbeat monitoring with automatic emergency stop
- Per-loco speed debouncing and global rate limiting
- Momentary (e.g. horn) and latching (e.g. lights) function buttons, as configured in the wiFRED
- LocoNet (serial, TCP, UDP multicast) and Z21 (UDP) command station adapters

## Prerequisites

Any command station that has LocoNet support or a network API (like the Z21 UDP protocol) can be used.
Only the USB-to-LocoNet serial option connects directly to the LocoNet bus;
all other adapters communicate with the command station via its own protocol or API.

Pick **one** of the following:

- **ROCO Z21** (or compatible) — communicates via the Z21 UDP API, no LocoNet bus connection needed.
- **Command station with LocoNet** — via a LoconetOverTcp server (e.g. JMRI, Rocrail, or LbServer) for TCP access.
- **Command station with LocoNet** — via a UDP multicast gateway (e.g. loconetd or GCA101 LocoBuffer-UDP).
- **USB-to-LocoNet device** — connects directly to the LocoNet bus via serial communication, e.g. the [RR-Cirkits LocoBuffer-NG](https://digira.se/webshop/ws-/products/locobuffer-ng).

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

### Running on a FREMO PiLocoBuffer

The [PiLocoBuffer](https://wiki.fremo-net.eu/doku.php?id=loconet:lbserver:pilocobuffer_active) is a Raspberry Pi with a LocoNet hat board and a built-in LbServer.
Since LbServer provides LocoNet over TCP, this app can run on the same Raspberry Pi
and connect locally:

```json
{
  "CommandStation": {
    "Type": "LocoNetTcp",
    "LocoNetTcp": {
      "Hostname": "127.0.0.1",
      "Port": 1234
    }
  }
}
```

Publish the app for your Raspberry Pi OS (`linux-arm` for 32-bit, `linux-arm64` for 64-bit),
copy it to the PiLocoBuffer, and run it alongside the LbServer.

## Installing and Running

### Linux

Install and run for the first time
1. unzip **linux-arm64.zip** -d wifredserver **<- or the zip for the platform you run on**
1. cd wifredserver
2. configure control station to use in **appsettings.json**
1. chmod +x Tellurian.Trains.WiFreds
1. ./Tellurian.Trains.WiFreds  **<- this starts the app, only thing needed when running later**

You may also consider to use autostart of the wiFRED Server.
This is operating system specific and not covered here.

### Windows

Install and run for the first time
1. unzip **linux-arm64.zip** -d wifredserver **<- or the zip for the platform you run on**
1. cd wifredserver
2. configure control station to use in **appsettings.json**
1. ./Tellurian.Trains.WiFreds  **<- this starts the app, only thing needed when running later**

You may also consider to use autostart of the wiFRED Server.
This is operating system specific and not covered here.

## Configuration

All settings are in `appsettings.json`:

| Section | Setting | Default | Description |
|---------|---------|---------|-------------|
| `WiFred` | `Port` | `12090` | TCP port for WiFred connections |
| `WiFred` | `HeartbeatTimeoutSeconds` | `10` | Seconds before inactive client triggers e-stop |
| `WiFred` | `ServiceName` | `WiFred Server` | mDNS service name |
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
using standard .NET configuration (e.g. `WiFred__Port=12345`).

## Supported Platforms

Published as self-contained single-file executables for:

- **Windows x64** (`win-x64`)
- **Linux ARM** (`linux-arm`) — e.g. Raspberry Pi (32-bit)
- **Linux ARM64** (`linux-arm64`) — e.g. Raspberry Pi (64-bit)

## Known Limitations

- LocoNet adapter (Serial, TCP, and UDP) supports functions F0-F12 only; F13-F28 require Z21.
- WiFred functions F0-F16; functions beyond F16 are protocol-supported but not used by WiFred.
- The server implements only the WiFred-required subset of the WiThrottle protocol.
