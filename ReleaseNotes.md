# Release Notes

## Version 1.3.8

### Bug Fixes

- Updated LocoNet/Z21 adapter packages to 1.7.1, fixing reversed F1-F4 function key bit order in LocoNet DIRF commands. This caused F1↔F4 and F2↔F3 to be swapped when controlling locomotives via USB-to-serial or other LocoNet connections.

## Version 1.3.7

### Web Dashboard

- Loco address editing is now restricted to inactive (red) addresses only. Active (green) addresses cannot be changed while in use.
- Added a hint below the page header explaining that red addresses can be clicked to change them.
- Automatic periodic refresh of wiFRED device configuration (default every 5 minutes per device), keeping battery level and loco addresses up to date without manual intervention.

### wiFRED WiFi Workaround

- Automatic fix for a suspected wiFRED firmware bug where the device caches the WiThrottle server address from a previously connected WiFi network, causing it to fail to connect when moving to a different network. When the server detects a wiFRED with multiple enabled WiFi networks, it temporarily disables the extra networks and restarts the device to force a fresh mDNS server discovery, then re-enables all networks so they remain available for the next location change.

### Command Station Connection

- Graceful handling of command station disconnection (e.g. USB adapter unplugged). The server retries twice and then shuts down cleanly instead of crashing with a stack trace.

### Diagnostics

- Added logging to the active loco address tracker for easier troubleshooting of address state changes.
- Added debug-level logging of incoming WiThrottle protocol messages.

### Documentation

- Added [Getting Started Guide](docs/getting-started.md) with plain-language setup instructions, connection diagrams, and troubleshooting — aimed at users who are not technically experienced.
- Streamlined the README to focus on technical reference, linking to the Getting Started Guide for installation steps.
- Added USB-to-Serial on Linux section to README (stable device paths, permissions, drivers, troubleshooting).

## Version 1.3.6

### Bug Fixes

- Fixed heartbeat timeout releasing loco addresses from the dashboard tracker, causing addresses to permanently show as red (inactive) even though the session was still alive and commands were being sent. This was most visible when using LocoNet via serial port, where slot requests can take up to 3 seconds each, blocking the TCP read loop long enough to trigger the heartbeat timeout.

### Build

- Fixed release workflow to keep `staticwebassets.endpoints.json` in published zips, as the server requires it at runtime.

### Documentation

- Reorganized README sections for better readability.

## Version 1.3.5

### Web Dashboard

- Battery level now shown as percentage (0–100%) using a realistic LiPo discharge curve, instead of a raw millivolt value. Low battery is highlighted in red.
- Configure button now shows a warning dialog before opening the wiFRED's configuration page, explaining that it may cause heartbeat timeouts and emergency stops while trains are running.

### Localization

- Added new resource strings for the configure warning dialog and cancel button in all languages (en, sv, de, da, nb).

## Version 1.3.4

### Bug Fixes

- Fixed device name, loco addresses, firmware version, and battery voltage not displaying on the web dashboard. The XML parsing did not match the actual wiFRED firmware XML structure (attribute-based values, `<LOCO>/<DCCadress>` elements).
- Added debug-level logging of raw XML received from wiFRED devices.
- Removed firmware version column from dashboard (available via the Configure button).
- Fixed inline loco address update: addresses above 127 now correctly set DCC long addressing mode on the wiFRED.
- Added Refresh button per device to re-fetch configuration from the wiFRED.

## Version 1.3.2

### Bug Fixes

- Fixed XML configuration not loading from wiFRED devices. The wiFRED firmware emits an uppercase `<?XML ...?>` declaration which violates the XML spec; the server now normalizes this before parsing.

## Version 1.3.1

### WiThrottle Protocol

- Loco acquisition now queries the command station for the current locomotive state (speed, direction, functions) and reports it back to the wiFRED, instead of always reporting a clean slate.

### Web Dashboard

- `Configure` button on each device row opens the wiFRED's own configuration page in a new tab.
- Active loco address indication: addresses currently acquired by a connected wiFRED session are shown with a green background, inactive addresses with a red background.

### Platforms

- Updated README with supported platforms.

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
