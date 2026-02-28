 # WiThrottle Server for WiFRED

 This document is a requirements specication for implementing a WiThrottle server with support just enough to
 enable WiFRED to connect and support all of its functions. The JMRI WiThrottle protocol supports more features
 that needed.

 ## Goal
 I want to implement a WiThottle server that let one or several WiFred (a variant of a WiThrottle) to connect. 
 - The commands from the WiFred should be forwarded to a control station using LocoNet. 
 - I already have a NuGet library   for that, source code here: https://github.com/tellurianinteractive/Tellurian.Train.Communications. 
 - The source   code for the WiFred is here: https://github.com/newHeiko/wiFred. 
 - The full WiThrottle protocol is here:
  https://www.jmri.org/help/en/package/jmri/jmrit/withrottle/Protocol.shtml. 
  
  I suggest a reverse engeneering to find out what we actually need to support. 

  I want to update this specification with details that we can use later for implementation. 
  
  Feel free to ask questions to clarify things.

## Reverse Engineering Results

The WiFred source code (https://github.com/newHeiko/wiFred, master branch, ESP32-S2) was analyzed
to determine the minimal WiThrottle protocol subset required.

### WiFred Capabilities Summary

- Supports up to **4 locomotives** simultaneously on a single throttle connection
- Uses only **multi-throttle** commands with throttle ID `T` (prefix `MT`)
- All 4 locos share the same speed (sent with wildcard `*`), direction is per-loco
- Functions F0-F16 supported per loco
- Does **not** use: turnouts, routes, roster, consisting, track power, programming, steal/share

### Protocol Message Format

- All messages are **newline-terminated** (`\n`)
- Array delimiter: `]\[`
- Sub-element delimiter: `}|{`
- Action delimiter: `<;>` separates command components in multi-throttle messages
- Loco address format: `L{number}` for long DCC address, `S{number}` for short DCC address

---

## Connection Lifecycle

### Phase 1: TCP Connection + Handshake

1. WiFred connects via TCP (discovers server via mDNS `_withrottle._tcp` or falls back to first IP on subnet)
2. WiFred sends: `N{throttleName}\n` (device name)
3. WiFred sends: `HU{macHexAddress}\n` (hardware unique ID, 6-byte MAC as hex without separators)
4. **Server must send**: `VN2.0\n` (protocol version -- WiFred checks `startsWith("VN2.0")`)

### Phase 2: Heartbeat Negotiation

5. **Server sends**: `*{seconds}\n` (heartbeat timeout interval, e.g. `*10`)
6. WiFred responds: `*+\n` (opt in to heartbeat monitoring)
7. WiFred calculates its keepalive interval as **40% of server timeout** (e.g. 4 seconds for a 10-second timeout)

### Phase 3: Normal Operation

8. WiFred sends heartbeat `*\n` at its calculated keepalive interval
9. Loco acquire/release, speed, direction, function commands as needed
10. **All incoming server data during normal operation is discarded** by WiFred

### Phase 4: Disconnect

11. WiFred sends: `Q\n`
12. TCP connection closed

### Connection Loss Recovery

- If TCP drops: WiFred re-acquires all active locos after reconnecting
- If WiFi drops: full reconnect from scratch

---

## Messages: WiFred -> Server (Client Commands)

### Handshake & Lifecycle

| Command | Format | Description |
|---------|--------|-------------|
| Throttle Name | `N{name}\n` | Human-readable device name |
| Hardware ID | `HU{macHex}\n` | 6-byte MAC address as hex |
| Heartbeat Opt-in | `*+\n` | Enable heartbeat monitoring |
| Heartbeat Keep-alive | `*\n` | Periodic heartbeat ping |
| Quit | `Q\n` | Disconnect, release all resources |

### Locomotive Acquisition & Release

| Command | Format | Description |
|---------|--------|-------------|
| Acquire Loco | `MT+{locoID}<;>{locoID}\n` | Request control of a locomotive |
| Release Loco | `MT-{locoID}<;>r\n` | Release a specific locomotive |

### Speed & Direction

| Command | Format | Description |
|---------|--------|-------------|
| Set Speed | `MTA*<;>V{speed}\n` | Speed 0-126, `*` = all locos on throttle |
| Set Direction | `MTA{locoID}<;>R1\n` | Forward |
| Set Direction | `MTA{locoID}<;>R0\n` | Reverse |
| Emergency Stop (all) | `MTA*<;>X\n` | E-stop all locos on throttle |
| Emergency Stop (one) | `MTA{locoID}<;>X\n` | E-stop specific loco |

### Function Control

| Command | Format | Description |
|---------|--------|-------------|
| Function Toggle On | `MTA{locoID}<;>F1{funcNum}\n` | Button press (capital F = toggle) |
| Function Toggle Off | `MTA{locoID}<;>F0{funcNum}\n` | Button release (capital F = toggle) |
| Force Function On | `MTA{locoID}<;>f1{funcNum}\n` | Force on (lowercase f) -- used during setup |
| Force Function Off | `MTA{locoID}<;>f0{funcNum}\n` | Force off (lowercase f) -- used during setup/reset |
| Set Momentary Mode | `MTA{locoID}<;>m1{funcNum}\n` | Function behaves as momentary |
| Set Locking Mode | `MTA{locoID}<;>m0{funcNum}\n` | Function behaves as latching |

### Speed Step Mode (optional)

| Command | Format | Description |
|---------|--------|-------------|
| Set Speed Steps | `MTA{locoID}<;>s{mode}\n` | Mode: `128`, `28`, `27`, `14`, etc. |

---

## Messages: Server -> WiFred (Server Responses)

WiFred only parses server responses during **handshake** and **loco acquisition**.
All other incoming data is discarded.

### Required Server Responses

| Response | Format | When | Description |
|----------|--------|------|-------------|
| Protocol Version | `VN2.0\n` | After client connects | Must start with `VN2.0` |
| Heartbeat Timeout | `*{seconds}\n` | After version | Timeout in seconds (e.g. `*10`) |

### Loco Acquisition Responses

After a loco is acquired (`MT+`), WiFred reads responses for up to 500ms, expecting:

| Response | Format | Description |
|----------|--------|-------------|
| Function State | `MTA{locoID}<;>F{0or1}{funcNum}\n` | Initial on/off state per function |
| Direction | `MTA{locoID}<;>R{0or1}\n` | Current direction (0=reverse, 1=forward) |
| Speed Step Mode | `MTA{locoID}<;>s{mode}\n` | Treated as end-of-response marker |

The server should send function states (F), direction (R), and speed step mode (s) in that order.
The `s` response signals to WiFred that the initial state dump is complete.

---

## What the Server Does NOT Need to Support

WiFred does not use any of these protocol features:

- Roster list (`RL`)
- Turnout control (`PTA`, `PTL`, `PTT`)
- Route control (`PRA`, `PRL`, `PRT`)
- Track power notifications (`PPA`)
- Fast clock (`PFT`)
- Web port (`PW`)
- Consist management (`RC`)
- Steal/share mechanism (`MTS`)
- Programming track
- Raw DCC packets (`D`)
- Function labels (`MTL`)

The server may safely omit all of these. WiFred discards any messages it doesn't recognize.

---

## Important Implementation Notes

1. **mDNS Discovery**: The server should advertise via mDNS as `_withrottle._tcp` for automatic discovery.
2. **Speed Range**: 0-126 (WiFred maps its potentiometer to this range).
3. **Speed Holdoff**: WiFred rate-limits speed commands to one per 150ms.
4. **E-Stop on Acquire/Release**: WiFred sends an emergency stop both when acquiring and releasing a loco.
5. **No Steal/Share**: WiFred does not handle steal prompts. The server should grant loco access immediately.
6. **Heartbeat**: If no command is received within the timeout, the server should emergency-stop all locos for that client.
7. **Concurrent Clients**: Multiple WiFred devices may connect simultaneously; each gets its own throttle session.
8. **Wildcard Address**: `*` in speed/e-stop commands means "all locos on this throttle."
9. **Capital F vs lowercase f**: Capital `F` is toggle (press/release), lowercase `f` is force (absolute set). Both must be handled.
10. **Function momentary/locking modes**: `m1`/`m0` configure whether functions are momentary or latching. The server should track this per function per loco.

---

## ILoco Interface -- Command Station Integration

Loco control is forwarded to the command station via the `ILoco` interface from
the NuGet package `Tellurian.Trains.Communications.Interfaces` (version 1.6.1).

**Namespace:** `Tellurian.Trains.Communications.Interfaces.Locos`

### ILoco Methods

```csharp
public interface ILoco
{
    Task<bool> DriveAsync(Address address, Drive drive, CancellationToken cancellationToken = default);
    Task<bool> EmergencyStopAsync(Address address, CancellationToken cancellationToken = default);
    Task<bool> SetFunctionAsync(Address address, Function locoFunction, CancellationToken cancellationToken = default);
}
```

### Related Types

| Type | Kind | Description |
|------|------|-------------|
| `Address` | struct | Loco address (0-9999). `IsLong` (>=128), `IsShort` (<128). Create with `Address.From(int)`. |
| `Drive` | struct | Combines `Direction` and `Speed`. |
| `Direction` | enum | `Forward`, `Backward` |
| `Speed` | struct | Create with `Speed.Set126(byte step)` for 126-step mode. `CurrentStep` property. |
| `Function` | struct | Create with `Function.On(Functions.Fn)`, `Function.Off(Functions.Fn)`, or `Function.Set(Functions.Fn, bool)`. |
| `Functions` | enum | `F0` through `F28` |

### WiFred Command -> ILoco Mapping

| WiFred Command | ILoco Call | Notes |
|---|---|---|
| `MTA{id}<;>V{speed}` + direction state | `DriveAsync(address, new Drive { Direction = dir, Speed = Speed.Set126(speed) })` | Speed and direction are combined into a single `Drive` call. The server must track current direction per loco to combine with speed-only updates. |
| `MTA{id}<;>R{0or1}` | `DriveAsync(address, new Drive { Direction = dir, Speed = currentSpeed })` | Direction change also requires sending current speed. R0 = `Backward`, R1 = `Forward`. |
| `MTA{id}<;>X` or `MTA*<;>X` | `EmergencyStopAsync(address)` | For wildcard `*`, call for each acquired loco. |
| `MTA{id}<;>F1{n}` or `f1{n}` | `SetFunctionAsync(address, Function.On((Functions)n))` | Both toggle-on and force-on map to `Function.On`. |
| `MTA{id}<;>F0{n}` or `f0{n}` | `SetFunctionAsync(address, Function.Off((Functions)n))` | Both toggle-off and force-off map to `Function.Off`. |

### Address Mapping

| WiFred Format | ILoco Mapping |
|---|---|
| `L{number}` (long DCC address) | `Address.From(number)` -- will have `IsLong = true` for addresses >= 128 |
| `S{number}` (short DCC address) | `Address.From(number)` -- will have `IsShort = true` for addresses < 128 |

### Server State Requirements

The server must maintain per-loco state to support the `ILoco` interface correctly:

- **Current speed**: Needed because direction changes require resending the current speed via `DriveAsync`.
- **Current direction**: Needed because speed changes require resending the current direction via `DriveAsync`.
- **Function states (F0-F16)**: Needed to respond to loco acquisition with initial function states.

### Known Limitation

The LocoNet adapter only supports functions **F0-F12** (returns `false` for F13+).
WiFred uses F0-F16, so functions F13-F16 will not work over LocoNet but will work over Z21.

---

## Message Throttling

### WiFred Client-Side Throttling (for reference)

WiFred already performs some rate limiting on its side:

| Mechanism | Value | Effect |
|-----------|-------|--------|
| **Speed holdoff** | 150ms | Max ~6.7 speed messages/sec per WiFred, with coalescing (only latest value sent) |
| **ADC sampling** | 16 samples x 4ms = 64ms cycle | New speed value computed every ~64ms |
| **ADC delta filter** | > 1 unit (of 253) | Suppresses potentiometer jitter |
| **Button debounce** | 50ms (5 x 10ms reads) | All buttons: F0-F8, ESTOP, direction, loco switches |
| **No function/direction holdoff** | -- | Function and direction messages are sent immediately after debounce |

Despite this, with multiple WiFred devices connected simultaneously, the aggregate message rate
to LocoNet can still be too high. The server adds its own throttling layer.

### Per-Loco Speed Throttling

Speed commands (`V{speed}`) are throttled **per loco** using two configurable criteria (both must be met to suppress):

1. **Time threshold**: A speed command is suppressed if less than the configured time has elapsed
   since the last forwarded speed command for that loco. Configurable range: **0.1 - 0.2 seconds**.
2. **Speed step threshold**: A speed command is suppressed if the speed change (absolute difference)
   since the last forwarded speed is less than or equal to a configured number of steps.

A speed command is forwarded when **either** threshold is exceeded:
- Enough time has elapsed, **or**
- The speed change is large enough.

This ensures smooth operation: small gradual turns are time-debounced, while large quick turns
are forwarded immediately.

**Important exceptions** -- these are always forwarded immediately, never throttled:
- Speed 0 (stop)
- Emergency stop (`X`)
- Direction changes (`R0`/`R1`)

When a speed command is suppressed, the latest value is retained. When the time threshold expires,
the most recent pending speed is forwarded (ensuring the final speed always reaches the command station).

### Global LocoNet Message Rate Limit

In addition to per-loco speed throttling, there is a **global rate limit** on all messages
sent to the command station, across all connected WiFred devices and all message types.

- Configurable maximum rate, default: **20 messages per second**.
- Applies to all `ILoco` calls: `DriveAsync`, `EmergencyStopAsync`, `SetFunctionAsync`.
- When the rate limit is hit, messages are queued and sent as capacity becomes available.
- **Emergency stops are exempt** from the global rate limit and are always sent immediately.

### Configuration Summary

| Setting | Default | Description |
|---------|---------|-------------|
| Speed time threshold | 150 ms | Minimum time between forwarded speed commands per loco |
| Speed step threshold | 2 | Minimum speed step change to forward immediately |
| Global message rate | 20/sec | Maximum ILoco calls per second across all clients |