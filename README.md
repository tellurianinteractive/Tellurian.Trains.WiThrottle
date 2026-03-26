# WiFred Server

A .NET 10 WiThrottle protocol server that enables [WiFred](https://github.com/newHeiko/wiFred) throttles
to control model trains via any command station that has LocoNet support or a network API (e.g. Roco Z21).

**Important: This implementation *only*** supports the part of the wiThrottle
protocol actually used by the **wiFRED**.

## References

- [wiFred source code](https://github.com/newHeiko/wiFred),
- [JMRI wiThrottle protocol](https://www.jmri.org/help/en/package/jmri/jmrit/withrottle/Protocol.shtml),
- [wiFRED user manual](https://newheiko.github.io/wiFred/documentation/docu_en.html)

## Features

- WiThrottle protocol v2.0 (wiFred-compatible subset)
- Up to 4 locos per WiFred, multiple concurrent wiFred connections
- Loco acquisition reports actual state (speed, direction, functions) from the command station
- mDNS service discovery (`_withrottle._tcp`)
- Heartbeat monitoring with automatic emergency stop
- Per-loco speed debouncing and global rate limiting
- Momentary (e.g. horn) and latching (e.g. lights) function buttons, as configured in the wiFRED
- LocoNet (serial, TCP, UDP multicast) and Z21 (UDP) command station adapters
- Web dashboard showing all connected wiFREDs with auto-refresh, including loco address conflict detection
- Inline editing of loco addresses from the web dashboard, with changes pushed directly to the wiFRED device
- Configure button to open each wiFRED's built-in configuration page
- Active/inactive loco address indication: green for addresses in use, red for idle

## Prerequisites

Any command station that has LocoNet support or a network API (like the Z21 UDP protocol) can be used.
Only the USB-to-LocoNet serial option connects directly to the LocoNet bus;
all other adapters communicate with the command station via its own protocol or API.

Pick **one** of the following:

- **ROCO Z21** (or compatible) — communicates via the Z21 UDP API, no LocoNet bus connection needed.
- **Command station with LocoNet** — via a LoconetOverTcp server (e.g. Rocrail or LbServer) for TCP access. Also JMRI supports LocoNet over TCP but also have its own wiThrottle server.
- **Command station with LocoNet** — via a UDP multicast gateway (e.g. loconetd or GCA101 LocoBuffer-UDP).
- **USB-to-LocoNet device** — connects directly to the LocoNet bus via serial communication, e.g. the [RR-Cirkits LocoBuffer-NG](https://digira.se/webshop/ws-/products/locobuffer-ng).

## Getting Started

For a step-by-step guide with connection diagrams, see the [Getting Started Guide](docs/getting-started.md).

Downloads are available under [Releases](https://github.com/tellurianinteractive/Tellurian.Trains.WiThrottle/releases).

### Supported Platforms

Published as self-contained single-file executables (no .NET installation needed):

| Platform | Download | Minimum OS |
|----------|----------|------------|
| Windows x64 | `wifred-server-win-x64.zip` | Windows 10 |
| Linux ARM | `wifred-server-linux-arm.zip` | Raspberry Pi OS 11+ (32-bit) |
| Linux ARM64 | `wifred-server-linux-arm64.zip` | Raspberry Pi OS 11+ (64-bit) |

The web dashboard works in any current version of Chrome, Edge, Firefox, or Safari.

## Configuration

All settings are in `appsettings.json`:


| Section                     | Setting                      | Default         | Description                                               |
| ----------------------------- | ------------------------------ | ----------------- | ----------------------------------------------------------- |
| `WiFred`                    | `Port`                       | `12090`         | TCP port for WiFred connections                           |
| `WiFred`                    | `HeartbeatTimeoutSeconds`    | `10`            | Seconds before inactive client triggers e-stop            |
| `WiFred`                    | `ServiceName`                | `WiFred Server` | mDNS service name                                         |
| `WiFred`                    | `OpenBrowserOnStart`         | `false`         | Open the web dashboard in the default browser on startup  |
| `Throttling`                | `SpeedTimeThresholdMs`       | `150`           | Min ms between forwarded speed commands per loco          |
| `Throttling`                | `SpeedStepThreshold`         | `2`             | Min speed step change to bypass time threshold            |
| `Throttling`                | `GlobalMessageRatePerSecond` | `20`            | Max command station messages/sec across all clients       |
| `CommandStation`            | `Type`                       | —              | `Serial`, `Z21`, `LocoNetTcp`, or `LocoNetUdp` (required) |
| `CommandStation:SerialPort` | `PortName`                   | `COM3`          | Serial port for LocoNet                                   |
| `CommandStation:SerialPort` | `BaudRate`                   | `57600`         | Baud rate for LocoNet                                     |
| `CommandStation:Z21`        | `Address`                    | `192.168.0.111` | Z21 IP address                                            |
| `CommandStation:Z21`        | `CommandPort`                | `21105`         | Z21 command UDP port                                      |
| `CommandStation:Z21`        | `FeedbackPort`               | `21106`         | Z21 feedback UDP port                                     |
| `CommandStation:LocoNetTcp` | `Hostname`                   | `localhost`     | LoconetOverTcp server hostname                            |
| `CommandStation:LocoNetTcp` | `Port`                       | `1234`          | LoconetOverTcp server port                                |
| `CommandStation:LocoNetUdp` | `MulticastGroup`             | `224.0.0.1`     | UDP multicast group address                               |
| `CommandStation:LocoNetUdp` | `ListenPort`                 | `1235`          | UDP multicast listen port                                 |
| `CommandStation:LocoNetUdp` | `SendAddress`                | `224.0.0.1`     | UDP multicast send address                                |
| `CommandStation:LocoNetUdp` | `SendPort`                   | `1235`          | UDP multicast send port                                   |
| `CommandStation:LocoNetUdp` | `ValidateChecksum`           | `true`          | Validate LocoNet checksums on received datagrams          |
| `WiFredDiscovery`           | `RefreshIntervalMinutes`     | `5`             | Minutes between automatic config re-reads per wiFRED      |

Settings can also be overridden via environment variables or command-line arguments
using standard .NET configuration (e.g. `WiFred__Port=12345`).

### Configuration Examples

Settings are in the `appsettings.json` file in the programs folder. You can have all different protocols defined in the file. The one used is the one given for the command station type.

#### Running with Serial Communication

Set the command station type to `Serial` and configure the serial port. The baud rate is the standard value used:

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

#### Running with ROCO Z21

Set the command station type to `Z21` and configure the Z21 network address. The command and feedback port numbers are standard for Z21:

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

#### Running with LocoNet over TCP

Connect to a LoconetOverTcp server (e.g. Rocrail or LbServer):

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

#### Running with LocoNet over UDP Multicast

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

#### Running on a FREMO PiLocoBuffer

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

Install the app for your Raspberry Pi OS (`linux-arm` for 32-bit, `linux-arm64` for 64-bit),
copy it to the PiLocoBuffer, and run it alongside the LbServer.

## wiFRED WiFi Configuration

The wiFRED firmware caches the WiThrottle server address after mDNS discovery and does not
clear this cache when switching between WiFi networks. This can cause connection failures
when moving a wiFRED between locations (e.g. club and home).

The server detects this situation automatically: when a wiFRED connects with multiple
WiFi networks enabled, the server temporarily disables the extra networks, restarts
the wiFRED to force a fresh server discovery, and then re-enables all networks.

## Known Limitations

- LocoNet adapter (Serial, TCP, and UDP) supports functions F0-F12 only; F13-F28 require Z21.
- WiFred functions F0-F16; functions beyond F16 are protocol-supported but not used by WiFred.
- The server implements only the WiFred-required subset of the WiThrottle protocol.

## USB-to-Serial on Linux

USB-to-serial adapters (used by LocoBuffer-USB, RR-CirKits LocoBuffer-NG, etc.) appear as
`/dev/ttyUSB0`, `/dev/ttyUSB1`, etc. The number is assigned in plug-in order and can change between reboots.

### Stable device paths

For a stable name that survives reboots, use the symlinks under `/dev/serial/by-id/`:

```bash
ls -la /dev/serial/by-id/
```

This gives paths like `usb-FTDI_FT232R_USB_UART_A12345-if00-port0` based on the adapter's chip and serial number.
Use the full path in `appsettings.json`:

```json
{
  "CommandStation": {
    "Type": "Serial",
    "SerialPort": {
      "PortName": "/dev/serial/by-id/usb-FTDI_FT232R_USB_UART_A12345-if00-port0",
      "BaudRate": 57600
    }
  }
}
```

### Permissions

The serial device is owned by the `dialout` group. The user running the server needs membership:

```bash
sudo usermod -aG dialout $USER
```

Log out and back in (or reboot) for the change to take effect.

### Drivers

Most LocoNet adapters use FTDI, CH340, or CP2102 chips. The drivers for these are included
in the default Raspberry Pi OS kernel — no extra installation needed.

### Troubleshooting

```bash
ls -la /dev/ttyUSB*          # check if the device appeared
ls -la /dev/serial/by-id/    # find the stable device name
dmesg | tail                  # kernel messages about the USB device
```
