# Heartbeat Timeout Investigation — wiFRED Config Page Interference

**Date:** 2026-03-24
**Status:** Under investigation

## Observed Behaviour

When running two trains (addresses 1375 and 1376) from one wiFRED device:

- Train 1375 (slot 1) stopped at regular intervals due to heartbeat timeouts triggering emergency stops
- Train 1376 (slot 2) appeared unaffected and tried to drag the stopped 1375
- Running 1375 alone in slot 1 reproduced the same stopping behaviour
- Running 1376 alone in slot 2 worked fine
- Both decoders are identical: ESU LokSound 5 with same configuration
- The wiFRED device's configuration page was open in a browser
- **Closing the config page immediately resolved the issue**

## wiFRED Firmware Facts

Source: [github.com/newHeiko/wiFred](https://github.com/newHeiko/wiFred) (master branch)

- **Processor**: ESP32-S2-WROOM (single-core, unlike the dual-core ESP32)
- **Single TCP connection**: All loco slots share **one** `WiFiClient client` instance (`locoHandling.cpp` ~line 107). All commands use WiThrottle multi-throttle addressing (`MTA` + per-loco ID) over this single connection.
- **Heartbeat**: The firmware sends `*\n` every `keepAliveTimeout` ms (set to 40% of server's timeout value). Speed commands (`MTA*<;>V{speed}`) also reset the heartbeat timer on the server.
- **HTTP server**: The device runs a built-in HTTP config server on port 80, on the same single core as the TCP client.
- **Cooperative multitasking**: The main loop calls `locoHandler()`, HTTP server handling, and other tasks sequentially on a single core.
- **Sequential loco processing**: `locoHandler()` processes locos 0–3 in order with `break` statements — only **one** loco state change is handled per loop iteration.

## Relevant Server Code

| Component | File | Key Lines | Purpose |
|-----------|------|-----------|---------|
| Heartbeat monitor | `WiFredTcpServer.cs` | 157–194 | Checks `LastActivity` per session every 5s; triggers e-stop if >10s inactive |
| Activity tracking | `ThrottleSession.cs` | 13, 31 | `LastActivity` timestamp, reset by `TouchActivity()` |
| Activity reset | `SessionHandler.cs` | 37 | Every incoming TCP message calls `TouchActivity()` |
| Emergency stop | `SessionHandler.cs` | 237–246 | `EmergencyStopAllAsync()` stops all locos in session, releases tracker only (locos stay in session) |
| Speed handling | `SessionHandler.cs` | 146–156 | `HandleSetSpeedAsync()` resolves target `*` to all locos in session |
| Timeout setting | `WiFredSettings.cs` | 6 | `HeartbeatTimeoutSeconds` default = 10 |
| Config page link | `Home.razor` | 66 | Links to `http://<device-ip>/` (the device's own HTTP server) |
| Config fetch | `WiFredDiscoveryService.cs` | 84–105 | `FetchConfigurationAsync()` — HTTP GET to device |

## Analysis

### Heartbeat mechanism

The WiFred Server monitors heartbeats **per TCP session** (one session per TCP connection). Every TCP message from the device (drive commands, heartbeat pings) resets the `LastActivity` timestamp via `TouchActivity()`. If no message arrives within 10 seconds, the server calls `EmergencyStopAllAsync()`, which:

1. Sends DCC emergency stop commands to **all** locos in the session
2. Calls `_tracker.ReleaseAll()` (updates the UI tracker only)
3. Does **not** remove locos from `_session.Locos` — they remain in the session dictionary
4. Resets `LastActivity` to prevent repeated e-stops

Because locos stay in the session, subsequent speed commands from the firmware (`MTA*<;>V{speed}`) will still be processed by `HandleSetSpeedAsync()` for all locos. The e-stop should therefore be **brief** — lasting only until the firmware sends its next speed command.

### Why the config page causes interference

The wiFRED device runs both services on a single core:

1. A **TCP client** to the WiFred Server (WiThrottle protocol, port 12090)
2. A **built-in HTTP server** for its configuration page (port 80)

When a browser has the config page open, it generates periodic HTTP traffic (page content, keep-alive, possible auto-refresh). On the single-core ESP32-S2, handling these HTTP requests **blocks or delays TCP heartbeat/drive messages**, causing the server-side heartbeat monitor to time out.

### Why only slot 1 appeared affected — unresolved

Since the firmware uses a **single TCP connection** and the server e-stops **all** locos on timeout, both trains should be affected equally. The slot-specific behaviour is not explained by the code alone.

**Possible explanations (in order of likelihood):**

1. **Only slot 1 was actually acquired by the server**: The firmware activates locos based on physical loco selection switches (`KEY_LOCOn`). If slot 2's switch was off, loco 1376 was never acquired and ran purely on its last DCC command (or was controlled by another throttle). Check the server log for `MT+` acquisition messages to confirm whether both locos were in the session.

2. **Re-acquisition storm on slot 1**: After an e-stop, the firmware calls `setESTOP()` which sends `MTA*<;>X\n`. The ESTOP flag is only cleared when the potentiometer reads zero. If the user's poti was above zero, the firmware **repeatedly sends e-stop** to all locos. Meanwhile, if the firmware tries to re-acquire slot 0 first (due to the sequential processing with `break`), the re-acquisition itself sends another e-stop (`MTA{locoId}<;>X`). Slot 0 gets repeatedly e-stopped during its acquisition sequence while slot 1 might already be stable.

3. **Both trains stopped briefly**: With identical decoders, both trains should stop. But the stops might be so brief (a fraction of a second) that only the lead train's stop was visible — the trailing train coasted through the brief pause.

4. **Separate testing was not simultaneous**: When the user tested "1376 alone in slot 2" vs "1375 alone in slot 1", the config page may not have been open during the slot 2 test (since the issue was not yet identified).

## Things to Investigate

### Clarify the observation

- [ ] **Check server logs for acquisition messages**: Search for `MT+` and `Acquired loco` entries. Were both L1375 and L1376 acquired in the same session? Or was only one present?
- [ ] **Check server logs for e-stop scope**: When heartbeat timeout fires, does the log show both addresses being e-stopped, or only one?
- [ ] **Confirm test conditions were identical**: When testing 1376 alone in slot 2, was the config page definitely still open?
- [ ] **Reproduce and observe both trains**: Run both trains again with config page open. Watch both carefully — does 1376 also pause briefly?

### Confirm the config page interference

- [ ] **Reproduce with config page open**: Run one train, open the config page, observe heartbeat timeouts in the log.
- [ ] **Reproduce with config page closed**: Same setup, config page closed — confirm no timeouts.
- [ ] **Check session count**: Confirm there is only one TCP session (`clientId`) from the wiFRED, not two.

### Characterise the interference

- [ ] **Identify which HTTP requests cause it**: Is it the initial page load, periodic refreshes, or keep-alive traffic? Try loading the page once then putting the browser tab in background.
- [ ] **Measure timing**: Log timestamps of heartbeat timeouts and correlate with HTTP request patterns from the browser.

## Potential Mitigations

### Server-side

- [ ] **Increase `HeartbeatTimeoutSeconds`**: A larger timeout (e.g. 15–20s) would tolerate brief pauses, but weakens the safety net for genuine disconnections.
- [ ] **Warn users in the dashboard**: Display a note that opening the wiFRED config page may interfere with train control.
- [ ] **Avoid linking to device config during active sessions**: Disable or hide the "Configure" button on the Home page when the device has active loco sessions.
- [ ] **Add detailed logging on heartbeat timeout**: Log exactly which locos are in the session and their current speed when the timeout fires.

### Firmware-side

The current wiFRED hardware uses the **ESP32-S2-WROOM** module (single-core). The ESP32-S2 supports FreeRTOS but has only one core, so dual-core pinning is not available:

- [ ] **Use `ESPAsyncWebServer`**: Replace any blocking HTTP server with the async variant (`ESPAsyncWebServer` + `async_tcp`), which handles requests via callbacks without blocking the main loop. This is the most practical fix for a single-core chip.
- [ ] **Non-blocking sockets via lwIP**: The ESP-IDF's `lwIP` stack supports non-blocking TCP sockets natively.
- [ ] **Prioritise TCP over HTTP in the FreeRTOS scheduler**: Give the WiThrottle TCP task a higher priority than the HTTP server task, so heartbeat messages are never starved.
- [ ] **Firmware update check**: Review the [wiFRED firmware repository](https://github.com/newHeiko/wiFred) for known issues or recent fixes related to multi-connection handling.
