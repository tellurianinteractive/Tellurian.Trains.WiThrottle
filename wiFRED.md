# wiFRED Documentation

Reference documentation for the [wiFRED](https://github.com/newHeiko/wiFred) wireless throttle.

## Loco Switches and Power Management

The four loco selection switches (LOCO1–LOCO4) serve as both the **power switch** and
the loco activation controls. There is no separate on/off button.

### Power On (Boot)

Moving **any** loco switch to the ON position physically connects the battery through a
hardware latch circuit, powering the ESP32-S2. The firmware cold-boots from scratch every
time — there is no software-configured GPIO wake-up from deep sleep.

On boot, the wiFRED:
1. Initializes GPIO, debounce timers, and ADC
2. Connects to a configured WiFi network
3. Discovers or connects to the configured WiThrottle server
4. Acquires each loco whose switch is in the ON position

### Loco Activation and Deactivation

During operation, toggling individual loco switches activates or deactivates locos
without rebooting:

- **Switch ON**: The loco is requested from the WiThrottle server (`MT+<id><;><id>`),
  function modes are sent, and force-on/force-off functions are applied.
- **Switch OFF**: The loco is released back to the server (`MT-<id><;>r`).
- **Any switch change** triggers an emergency stop to all attached locos as a safety measure.

### Power Off (Shutdown Sequence)

When **all four** loco switches are turned OFF, the wiFRED enters a multi-step shutdown:

| Step | State | Duration | What happens |
|------|-------|----------|--------------|
| 1 | `STATE_LOCOS_OFF` | 6 seconds | Grace period — if a switch is turned back ON, the wiFRED returns to online mode without rebooting |
| 2 | `STATE_LOWPOWER_WAITING` | 100 ms | Disconnects from WiThrottle server and shuts down WiFi. Red LED blinks very slowly (1 ms on / 250 ms cycle) |
| 3 | `STATE_LOWPOWER` | up to 60 seconds | Waits for the hardware keep-alive circuit to discharge, then calls `ESP.deepSleep(0)` (indefinite sleep) |

If a loco switch is turned back ON during `STATE_LOWPOWER`, the firmware performs
a full reboot (`ESP.restart()`).

### Other Sleep Triggers

- **Inactivity**: After **3 hours** with no speed changes, function presses, or loco
  registrations, the wiFRED emergency-stops all locos and enters the shutdown path.
- **Empty battery**: If the battery voltage drops too low, the wiFRED enters
  `STATE_LOWPOWER_WAITING` directly (after gracefully releasing any active locos).

### LED Indicators

| State | LED pattern |
|-------|-------------|
| Booting with a loco switch ON | Red LED: 100 ms on / 200 ms cycle |
| Booting with all switches OFF | Battery level shown on all 3 LEDs (green/yellow/red) |
| Shutting down | Red LED: 1 ms on / 250 ms cycle (barely visible) |


## Configuration

### Function Button Modes

Each of the 17 functions (F0-F16) per loco slot can be configured independently
via the wiFRED web interface. The available modes are:

| Value | Mode | Description |
|-------|------|-------------|
| 0 | THROTTLE | Default behavior, throttle controlled |
| 1 | THROTTLE_MOMENTARY | Throttle controlled, forced momentary (active while held) |
| 2 | THROTTLE_LOCKING | Throttle controlled, forced latching (toggle on press) |
| 3 | THROTTLE_SINGLE | Throttle controlled only if this is the only loco in the consist |
| 4 | ALWAYS_ON | Function forced always on at loco acquisition |
| 5 | ALWAYS_OFF | Function forced always off at loco acquisition |
| 6 | IGNORE | Function key ignored |

At loco acquisition, wiFRED sends `m1<n>` (momentary) or `m0<n>` (latching) for each function
based on the configured mode. During operation, physical button presses send `F1<n>` (press)
and `F0<n>` (release) events. Force on/off uses `f1<n>` and `f0<n>`.

### Web Server

The wiFRED runs a web server on **port 80**. All configuration changes are made via
HTTP GET requests with query parameters (the HTML forms use `method="get"`).
No authentication is required.

### Accessing the Web Server

- **Config AP mode**: Hold SHIFT (yellow) button 5 seconds at boot. The wiFRED creates
  its own WiFi AP named `wiFred-configXXXXXX`. A captive DNS portal redirects to the
  web server. mDNS name: `config.local`.
- **Config Station mode**: Hold F0 button 5 seconds while connected to WiFi. The web server
  is accessible at `<throttleName>.local` via mDNS.

### Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /index.html?<params>` | All configuration writes |
| `GET /api/getConfigXML` | Read entire configuration as XML |
| `GET /funcmap.html?loco=<1-4>&f0=<val>&...&f16=<val>` | Function mapping configuration |
| `GET /scanWifi.html` | Scan available WiFi networks |
| `GET /restart.html` | Restart the device |
| `GET /resetConfig.html?reallyReset=on` | Factory reset |
| `GET /flashred.html?count=N` | Blink red LED for identification |
| `GET /update` | OTA firmware update |

### Device Discovery

- **mDNS**: Advertises as `<throttleName>.local` with HTTP service on TCP port 80.
- **UDP broadcast**: Sends the string `"wiFred"` on UDP port **51289** after connecting to WiFi.

### Configuration Parameters

All parameters are sent as query strings to `GET /index.html`.

#### WiThrottle Server

| Parameter | Type | Description |
|-----------|------|-------------|
| `loco.serverName` | string | WiThrottle server hostname or IP |
| `loco.serverPort` | integer | WiThrottle server port (default: 12090) |
| `loco.automatic` | presence | If present, enables Zeroconf/Bonjour auto-discovery |

#### Loco Configuration

| Parameter | Type | Description |
|-----------|------|-------------|
| `loco` | integer (1-4) | Which loco slot to configure |
| `loco.address` | integer | DCC address (-1 to disable, 1-10239 long, 1-127 short) |
| `loco.longAddress` | presence | If present, uses long (extended) DCC address |
| `loco.direction` | integer | 0 = Normal, 1 = Reverse, 2 = Don't change |
| `loco.mode` | string | Speed step mode: `"128"`, `"28"`, `"27"`, `"14"`, `"motorola_28"`, `"tmcc_32"`, `"incremental"`, `"1"`, `"2"`, `"4"`, `"8"`, `"16"`, or `""` (do not set) |

#### Function Mapping

| Parameter | Type | Description |
|-----------|------|-------------|
| `loco` | integer (1-4) | Which loco slot |
| `f0` - `f16` | integer (0-6) | Function mode per the table above |

#### WiFi Network Management

| Parameter | Type | Description |
|-----------|------|-------------|
| `wifiSSID` + `wifiKEY` | string | Add a WiFi network |
| `remove=<ssid>` | string | Remove a WiFi network |
| `disable=<ssid>` | string | Disable a WiFi network |
| `enable=<ssid>` | string | Enable a WiFi network |

#### General

| Parameter | Type | Description |
|-----------|------|-------------|
| `throttleName` | string | Device name (also used for mDNS) |
| `centerSwitch` | integer | Center switch behavior: -2 = ignore, -1 = zero speed, 0-16 = activate function |

#### Calibration

| Parameter | Type | Description |
|-----------|------|-------------|
| `resetPoti=true` | | Reset speed potentiometer calibration |
| `newVoltage=<millivolts>` | integer | Correct battery voltage calibration |

### XML Configuration API

`GET /api/getConfigXML` returns the complete device configuration as XML, including:

- `<throttleName>` - device name
- `<localIP>` - current IP address
- `<firmwareRevision>` - firmware version
- `<batteryVoltage>` and `<batteryLow>` - battery status
- `<WiFi>` - connection status, SSID, signal strength, MAC address
- `<LOCOS>` - all 4 loco slots with address, mode, direction, long address flag, and F0-F16 mappings
- `<NETWORKS>` - all configured WiFi networks with SSIDs, keys, and enabled/disabled status
- `<LOCOSERVER>` - server name, port, and automatic discovery flag
- `<centerSwitch>` - center switch configuration


## Communication Timing and Rate Limiting

### Speed Command Rate Limiting

Speed commands are rate-limited by a holdoff period of **150 ms** (`SPEED_HOLDOFF_PERIOD`).
A speed command is only sent if:
- The speed value has actually changed, AND
- At least 150 ms has elapsed since the last speed update.

Sending a speed command also resets the heartbeat timer, preventing redundant keepalive
messages right after a speed update.

### Speed Potentiometer Filtering

The potentiometer reading goes through multiple filtering stages:

1. **ADC sampling**: A timer fires every **2 ms**, alternating between speed and battery readings.
   **16 samples** are averaged per reading, giving an effective averaging window of ~64 ms.
2. **Dead band**: A new speed is only forwarded if the averaged reading changed by more than
   1 step (out of ~126 usable steps, i.e. ~0.8% of full scale).
3. **Auto-calibration hysteresis**: Calibration min/max values require **16 consecutive**
   readings past the limit before updating, preventing transient spikes from affecting calibration.
4. **Gradual max reduction**: A separate timer reduces the max calibration by 1 every **10 seconds**,
   ensuring the throttle always eventually reaches zero speed.

### Button Debouncing

All buttons are debounced by a timer that checks every **10 ms**. A button must read
consistently in the new state for **4 consecutive checks** (= **40 ms**) before the
state change is accepted. This applies to both press and release transitions.

### Heartbeat / Keepalive

- During connection setup, the wiFRED reads the server's announced timeout and sends `*+\n`
  to enable heartbeat mode.
- The wiFRED uses **40% of the server's timeout** as its own heartbeat interval
  (it multiplies the server's seconds value by 400, not 1000).
- Default keepalive timeout (before server negotiation): **5000 ms**.
- The heartbeat message (`*\n`) is only sent when there has been no other traffic
  (speed, function, etc.) for the full keepalive interval.

### TCP Settings

- `client.setNoDelay(true)` — disables Nagle's algorithm so commands are sent immediately.
- `client.setTimeout(10)` — 10 ms socket timeout.
- No message queuing or batching; each command is sent as an individual `client.print()` call.

### Reconnection

- If the TCP connection drops, all active locos are flagged for re-acquisition and the
  device retries after **60 seconds**.
- WiFi connection timeouts: **20 seconds** per individual network attempt,
  **60 seconds** total before giving up.
- If auto-discovery is enabled, the wiFRED uses mDNS to find a `_withrottle._tcp` service.
  If no mDNS result is found, it falls back to gateway IP + 1 (for LNWI/DCC-EX devices).

### Inactivity Timeout

After **3 hours** (10,800,000 ms) with no user activity, the wiFRED automatically enters
sleep mode.

### Timing Constants Summary

| Constant | Value | Purpose |
|----------|-------|---------|
| Speed holdoff | 150 ms | Min time between speed commands |
| ADC sample interval | 2 ms | How often the potentiometer is sampled |
| ADC samples averaged | 16 | Samples per speed reading (~64 ms window) |
| Button debounce interval | 10 ms | How often key state is checked |
| Button debounce count | 4 | Consecutive reads needed (= 40 ms) |
| Default keepalive | 5000 ms | Heartbeat interval before server negotiation |
| Keepalive multiplier | 40% of server timeout | Safety margin for heartbeat |
| Calibration overshoot count | 16 | Readings needed to update calibration |
| Calibration max reduction | every 10 s | Gradual potiMax reduction |
| Center function e-stop | 500 ms | Time in center to allow direction change |
| All function reset | 5000 ms | Hold ESTOP to reset all functions |
| Inactivity timeout | 3 hours | Auto-sleep if no activity |
| Reconnection delay | 60 s | Wait before reconnecting after disconnect |
| Single network timeout | 20 s | Timeout for one WiFi connection attempt |
| Total network timeout | 60 s | Total WiFi connection timeout |

### Hardware Buttons

- **ESTOP (red) held 5s at boot**: Factory reset (deletes all configuration)
- **SHIFT (yellow) held 5s at boot**: Enter Config AP mode
- **F0 held 5s while connected**: Enter Config Station mode

### Persistence

Configuration is stored on the ESP32 SPIFFS filesystem as JSON files:

| File | Contents |
|------|----------|
| `/server.txt` | WiThrottle server name, port, automatic flag |
| `/config.txt` | Throttle name, center switch setting |
| `/wifi0.txt`, `/wifi1.txt`, ... | WiFi networks (SSID, PSK, disabled flag) |
| `/loco1.txt` - `/loco4.txt` | Loco configurations with function mappings |
| `/calibration.txt` | Potentiometer min/max, battery factor |
