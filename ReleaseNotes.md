# Release Notes

## Version 1.3.0

### Web Dashboard
- Expanded device table with firmware version and battery voltage columns.
- Each of the 4 loco address slots shown as a separate column (Loco 1–4).
- Inline editing of loco addresses: click an address to edit, save to push the change to the wiFRED via its HTTP API, then re-fetch the XML configuration to confirm.

## Version 1.2.0

### Web Dashboard
- Built-in Blazor Server web dashboard showing all connected wiFRED devices.
- Displays device name, IP address, configured loco addresses, and last seen timestamp.
- Highlights loco address conflicts when multiple wiFREDs control the same loco.
- Auto-refreshes every 5 seconds.
- Localized UI with support for English, Swedish, Norwegian, Danish, and German (detected from browser language).
- Optional auto-open browser on startup via `WiFred:OpenBrowserOnStart` setting.

## Version 1.1.0

### wiFRED Device Discovery
- UDP discovery service that listens for wiFRED broadcast messages on port 51289.
- Automatically fetches and stores device XML configuration from discovered devices.
- Tracks active/inactive state: devices are marked inactive when their WiFred TCP session ends.
- Detects loco address conflicts when multiple wiFRED devices are configured with the same address.

## Version 1.0.0

Initial release of the WiFred server for wiFRED throttles.

### WiThrottle Protocol
- Implements WiThrottle protocol v2.0, the subset used by wiFRED devices.
- Loco acquire/release, speed, direction, emergency stop, and function commands.
- Wildcard target (`*`) for controlling all acquired locos with a single command.
- Up to 4 locos per wiFRED, with multiple concurrent wiFRED connections.
- Functions F0-F28 with momentary and latching modes.

### Command Station Support
- **Roco Z21** (UDP) — direct network communication, no LocoNet bus needed.
- **LocoNet via serial port** — USB-to-LocoNet devices such as RR-Cirkits LocoBuffer-NG.
- **LocoNet over TCP** — connects to LoconetOverTcp servers (JMRI, Rocrail, LbServer).
- **LocoNet over UDP multicast** — via multicast gateways (loconetd, GCA101 LocoBuffer-UDP).

### Safety
- Heartbeat monitoring with configurable timeout and automatic emergency stop.
- Emergency stop on client disconnect and loco release.

### Performance
- Per-loco speed debouncing with configurable time and step thresholds.
- Global rate limiting (token bucket) to avoid overloading the command station.

### Network
- mDNS service advertisement (`_withrottle._tcp`) for automatic server discovery.

### Platforms
- Self-contained single-file executables for Windows x64, Linux ARM, and Linux ARM64 (Raspberry Pi).
- GitHub Actions release workflow for automated builds.
