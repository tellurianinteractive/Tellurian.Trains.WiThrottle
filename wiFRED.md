# wiFRED Protocol Reference

This document covers the WiThrottle protocol subset used by the
[wiFRED](https://github.com/newHeiko/wiFred) wireless throttle,
as relevant to this server implementation.

For device-specific details (hardware, configuration, firmware behaviour),
see the [official wiFRED documentation](https://newheiko.github.io/wiFred/documentation/docu_en.html).

## Device Discovery

### mDNS

The wiFRED can auto-discover a WiThrottle server by querying for
`_withrottle._tcp` via mDNS. The server advertises this service
so that wiFRED devices on the same network can find it automatically.

### UDP Broadcast

After connecting to WiFi, the wiFRED broadcasts the string `"wiFred"` on
UDP port **51289**. The server's `WiFredDiscoveryService` listens on this
port and, on receiving the broadcast, fetches the device's configuration
via `GET http://{deviceIP}/api/getConfigXML` to detect loco address
conflicts between connected devices.

## Protocol Messages

All multi-throttle commands use the format `MT{action}{target}<;>{command}`,
where `<;>` is the literal delimiter. Messages are newline-terminated.

### Connection Handshake

| Direction | Message | Purpose |
|-----------|---------|---------|
| Server → Client | `VN2.0` | Protocol version |
| Server → Client | `*{seconds}` | Heartbeat timeout |
| Client → Server | `N{name}` | Throttle name |
| Client → Server | `HU{macHex}` | Hardware identifier |
| Client → Server | `*+` | Opt in to heartbeat monitoring |

### Loco Acquire / Release

| Message | Description |
|---------|-------------|
| `MT+{locoId}<;>{locoId}` | Acquire a loco. The server responds with current function states, direction, and speed step mode. |
| `MT-{locoId}<;>r` | Release a loco. The server emergency-stops it. |

Loco IDs use the format `L{number}` for long (extended) DCC addresses
and `S{number}` for short addresses.

### Speed, Direction, and Emergency Stop

| Message | Description |
|---------|-------------|
| `MTA{target}<;>V{speed}` | Set speed (0–126) |
| `MTA{target}<;>R{0\|1}` | Set direction (0 = reverse, 1 = forward) |
| `MTA{target}<;>X` | Emergency stop |

The `{target}` is either a specific loco ID or `*` to address all
acquired locos.

### Functions

| Message | Description |
|---------|-------------|
| `MTA{target}<;>F{0\|1}{n}` | Button press (1) / release (0) for function *n* |
| `MTA{target}<;>f{0\|1}{n}` | Force function *n* on (1) or off (0) |
| `MTA{target}<;>m{0\|1}{n}` | Set function *n* mode: 0 = latching, 1 = momentary |

For latching functions, the server toggles the function state on
button-press (`F1`) and ignores button-release (`F0`).
For momentary functions, the server passes the button state directly.

### Speed Step Mode

| Message | Description |
|---------|-------------|
| `MTA{target}<;>s{mode}` | Declare speed step mode |

The wiFRED may send a speed step mode string such as `128`, `28`, `14`,
or others. The server currently acknowledges but does not act on this message.

### Session Control

| Message | Description |
|---------|-------------|
| `*+` | Enable heartbeat monitoring |
| `*` | Heartbeat keepalive |
| `Q` | Quit — server emergency-stops and releases all locos |

## Heartbeat

During connection setup the server announces its heartbeat timeout
(in seconds). The wiFRED opts in by sending `*+` and then uses **40 %
of the server's announced timeout** as its own keepalive interval.
If the server receives no traffic within the timeout period it
emergency-stops the session's locos.

## Speed Command Rate Limiting

The wiFRED rate-limits speed commands with a holdoff of **150 ms**
(`SPEED_HOLDOFF_PERIOD`). The server applies its own speed throttling
(configurable via `ThrottlingSettings.SpeedTimeThresholdMs`, default
150 ms) to smooth out bursts from any client.
