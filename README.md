# WiFred Server

A .NET 10 WiThrottle protocol server that enables [WiFred](https://github.com/newHeiko/wiFred) throttles
to control model trains via any command station that has LocoNet support or a network API (e.g. Roco Z21).

**Important: This implementation *only*** supports the part of the wiThrottle
protocol acually used by the **wiFRED**.

## References

- [wiFred source code](https://github.com/newHeiko/wiFred),
- [JMRI wiThrottle protocol](https://www.jmri.org/help/en/package/jmri/jmrit/withrottle/Protocol.shtml),
- [wiFRED user manual](https://newheiko.github.io/wiFred/documentation/docu_en.html)

## Features

- WiThrottle protocol v2.0 (wiFred-compatible subset)
- Up to 4 locos per WiFred, multiple concurrent wiFred connections
- Loco acquisition reports actual state (speed, direction, functions) from the command station
- mDNS service discovwry (`_withrottle._tcp`)
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

### Supported Platforms

Published as self-contained single-file executables for:

- **Windows x64** (`win-x64`)
- **Linux ARM** (`linux-arm`) — e.g. Raspberry Pi (32-bit)
- **Linux ARM64** (`linux-arm64`) — e.g. Raspberry Pi (64-bit)

### System Requirements

#### Server (runs the wiFRED Server application)

The app is built on .NET 10 and published as a self-contained executable, so no separate .NET installation is needed. However, the operating system must meet .NET 10's minimum requirements:

| OS | Minimum Version |
|----|-----------------|
| Windows (x64) | Windows 10 or Windows Server 2012 R2 |
| Linux ARM/ARM64 | Raspberry Pi OS 11 (Bullseye) or later, Ubuntu 22.04+, Debian 11+ |
| macOS | macOS 15 "Sequoia" or later (no pre-built binary; build from source) |

Windows 7 and Windows 8.1 are **not supported** by .NET 10.

A detailed .NET 10 OS support list can be found [here](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

#### Web Dashboard (browser)

The web dashboard uses Blazor Server with SignalR over WebSockets.
Any device on the network can access it — it does not need to run on the same machine as the server.

| Browser | Minimum Version |
|---------|-----------------|
| Google Chrome | Current version |
| Microsoft Edge | Current version |
| Mozilla Firefox | Current version |
| Apple Safari | Current version |

Internet Explorer is **not supported**. Microsoft dropped Blazor support for IE starting with ASP.NET Core 5.0.

### Installing and Running

You find releases under [Releases](https://github.com/tellurianinteractive/Tellurian.Trains.WiThrottle/releases) on the repository's root page.

#### Linux

Install and run for the first time

1. Download **wifred-server-linux-arm.zip** for 32-bit or **wifred-server-linux-arm64.zip** for 64-bit
2. unzip **wifred-server-*.zip** -d wifredserver
3. cd wifredserver
4. configure control station to use in **appsettings.json**
5. chmod +x Tellurian.Trains.WiFreds
6. ./Tellurian.Trains.WiFreds  **<- this starts the app, only thing needed when running later**

You may also consider to use autostart of the wiFRED Server.
This is operating system specific and not covered here.

#### Windows

Install and run for the first time

1. Download **wifred-server-win-x64.zip**.
2. unzip **wifred-server-win-x64.zip** -d wifredserver
3. cd wifredserver
4. configure control station to use in **appsettings.json**
5. ./Tellurian.Trains.WiFreds  **<- this starts the app, only thing needed when running later**

You may also consider to use autostart of the wiFRED Server.
This is operating system specific and not covered here.

## Web Dashboard

The server includes a built-in web dashboard that shows all currently connected wiFRED devices.
The page auto-refreshes every 5 seconds and displays:

- Device name, IP address, and battery level as percentage (with low-battery warning)
- All 4 loco address slots as individual columns, color-coded: green for addresses actively controlled by a wiFRED session, red for idle addresses
- Last seen timestamp
- Configure button with a safety warning before opening the wiFRED's configuration page (opening it while trains are running may interfere with heartbeats)
- Loco address conflicts (when multiple wiFREDs control the same loco)

Loco addresses can be edited inline: click an address value to open an edit field,
change the value, and click Save. The server sends the update to the wiFRED
and re-reads its configuration to confirm the change.

It is possible to configure in `appsettings.json` that the web dashboard should autostart. If you are running headless (without screen) this setting should
remain `false`.

The web UI is available at `http://<server-address>:5000` by default.
The web port can be configured via the `Urls` setting in `appsettings.json`
or via the `--urls` command-line argument:

```bash
./Tellurian.Trains.WiFreds --urls http://*:8080
```

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
