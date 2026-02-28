# WiThrottle Server Implementation Plan

## Context

We need to implement a WiThrottle server that allows WiFred throttle devices to connect via TCP and forward locomotive commands to a command station (LocoNet or Z21) using the existing `ILoco` interface from `Tellurian.Trains.Communications.Interfaces`. The requirements specification (reverse-engineered from WiFred source code) defines a minimal protocol subset. This is a fresh .NET 10 Worker Service project.

---

## Project Structure

```
Tellurian.Trains.WiThrottles/
  Tellurian.Trains.WiThrottles.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
  Configuration/
    WiThrottleSettings.cs
    ThrottlingSettings.cs
    CommandStationSettings.cs
  Protocol/
    WiThrottleMessage.cs          -- Discriminated union of all parsed messages
    WiThrottleParser.cs           -- Static parser: string line -> WiThrottleMessage
    LocoAddress.cs                -- WiFred "L1234"/"S5" <-> Address conversion
  Sessions/
    LocoState.cs                  -- Per-loco mutable state (speed, direction, functions)
    ThrottleSession.cs            -- Per-client session (name, HW ID, acquired locos, heartbeat)
    SessionHandler.cs             -- Protocol state machine: message -> ILoco calls + responses
  Throttling/
    SpeedThrottler.cs             -- Per-loco speed debouncing (time + step thresholds, trailing edge)
    GlobalRateLimiter.cs          -- Token bucket rate limiter (default 20/sec, e-stop exempt)
    ThrottledLocoController.cs    -- Wraps ILoco with both throttling layers
  Server/
    WiThrottleTcpServer.cs        -- BackgroundService: TCP listener, per-client tasks, heartbeat monitor
    MdnsAdvertiser.cs             -- BackgroundService: advertise _withrottle._tcp via mDNS
  Development/
    LoggingLocoController.cs      -- ILoco mock that logs instead of sending to hardware
```

---

## Implementation Phases

### Phase 1: Project Scaffolding & Configuration

**Files:** `.csproj`, `appsettings.json`, `appsettings.Development.json`, `Program.cs` (minimal), configuration records

- Worker Service SDK targeting `net10.0`, nullable enabled
- NuGet dependencies: `Tellurian.Trains.Adapters.LocoNet` v1.6.1, `Tellurian.Trains.Adapters.Z21` v1.6.1, `Tellurian.Trains.Protocols.LocoNet` v1.6.1, `Makaretu.Dns.Multicast` (for mDNS)
- Configuration records (sealed records with defaults):
  - `WiThrottleSettings`: Port (12090), HeartbeatTimeoutSeconds (10), ServiceName
  - `ThrottlingSettings`: SpeedTimeThresholdMs (150), SpeedStepThreshold (2), GlobalMessageRatePerSecond (20)
  - `CommandStationSettings`: Type (LocoNet/Z21), serial port settings, Z21 address/ports

### Phase 2: Protocol Layer

**Files:** `WiThrottleMessage.cs`, `WiThrottleParser.cs`, `LocoAddress.cs`

- Abstract record `WiThrottleMessage` with sealed derived types for each message kind
- Static `WiThrottleParser.Parse(string line)` using pattern matching
- Messages: ThrottleName, HardwareId, HeartbeatOptIn, Heartbeat, Quit, AcquireLoco, ReleaseLoco, SetSpeed, SetDirection, EmergencyStop, SetFunction, SetFunctionMode, SetSpeedSteps, Unknown
- `LocoAddress` helper: parse `L{n}`/`S{n}` to `Address.From(n)` and back

### Phase 3: Session State

**Files:** `LocoState.cs`, `ThrottleSession.cs`

- `LocoState`: Address, LocoId string, Speed (byte), Direction, FunctionStates[17], FunctionMomentary[17]
- `ThrottleSession`: Name, HardwareId, HeartbeatEnabled, LastActivity, Dictionary of acquired locos (up to 4)

### Phase 4: Development Mock

**File:** `LoggingLocoController.cs`

- Implements `ILoco` with logging (follows `LoggingYardController` pattern from YardController.Web)
- Enables testing without hardware

### Phase 5: Throttling Layer

**Files:** `SpeedThrottler.cs`, `GlobalRateLimiter.cs`, `ThrottledLocoController.cs`

- **SpeedThrottler** (per-loco): Forward when either time >= threshold OR step change > threshold. Trailing edge timer sends final pending value. Speed 0 always forwarded immediately.
- **GlobalRateLimiter**: Token bucket (20/sec default). Emergency stops exempt. Queues excess messages.
- **ThrottledLocoController** (singleton): Wraps `ILoco`. Speed changes go through both layers. Direction/function changes go through global only. E-stops bypass everything.

### Phase 6: Session Handler

**File:** `SessionHandler.cs`

- Pattern-matches `WiThrottleMessage` to protocol actions
- `HandleAcquireAsync`: Creates LocoState, returns multi-line response (F0-F16 states, direction, speed step mode `s128` as end marker)
- `HandleSpeedAsync`: Wildcard `*` applies to all locos. Updates state, calls `ThrottledLocoController.DriveAsync`
- `HandleDirectionAsync`: Updates state, calls `DriveAsync` with current speed
- `HandleEmergencyStopAsync`: Calls `EmergencyStopAsync` for target or all locos
- `HandleFunctionAsync`: Updates state, calls `SetFunctionAsync`
- `HandleQuitAsync`: E-stops all, releases all
- Returns response string(s) or null

### Phase 7: TCP Server

**File:** `WiThrottleTcpServer.cs`

- `BackgroundService` listening on configurable port
- Per-client async task (fire-and-forget with error handling)
- Handshake: send `VN2.0\n` then `*{seconds}\n`
- Read loop: `StreamReader.ReadLineAsync` -> parse -> handle -> write response
- Heartbeat monitor: periodic check of `LastActivity`, e-stop all locos on timeout
- Cleanup on disconnect: e-stop and release all locos

### Phase 8: mDNS Advertisement

**File:** `MdnsAdvertiser.cs`

- `BackgroundService` advertising `_withrottle._tcp` service via `Makaretu.Dns.Multicast`
- Can be deferred -- WiFred supports manual IP fallback

### Phase 9: Program.cs Composition Root

- Options pattern: `Configure<WiThrottleSettings>`, `Configure<ThrottlingSettings>`, `Configure<CommandStationSettings>`
- Environment switching (follows YardController.Web pattern):
  - **Development**: `LoggingLocoController` as `ILoco`
  - **Production**: LocoNet (`SerialPortAdapter` + `SerialDataChannel` + `LocoNet.Adapter`) or Z21 (`UdpDataChannel` + `Z21.Adapter`) based on config
- Register `ThrottledLocoController` singleton
- Register `WiThrottleTcpServer` and `MdnsAdvertiser` as hosted services
- Start adapter receive loop (via a `CommandStationInitializer : BackgroundService`)

### Phase 10: Solution File

- Update `Tellurian.Trains.WiThrottles.slnx` with project reference

---

## Key Design Decisions

1. **Single `ThrottledLocoController` singleton** -- global rate limiter must be shared across all clients
2. **`SessionHandler` returns strings, doesn't own the stream** -- enables unit testing without TCP
3. **Sealed classes everywhere** -- follows developer convention, enables JIT optimization
4. **Records for config, classes for mutable state** -- idiomatic .NET pattern
5. **No steal/share handling** -- WiFred doesn't support it, server grants access immediately

## Important Notes

- LocoNet/Z21 adapters require `StartReceiveAsync()` before use -- a `CommandStationInitializer` BackgroundService handles this
- Multi-line acquisition responses must be written atomically to avoid interleaving between clients
- `DriveAsync` requires both speed and direction -- server tracks both per-loco to combine partial updates

---

## Verification

1. **Build**: `dotnet build` succeeds
2. **Development mode**: Run in Development environment, connect with telnet/nc to port 12090, manually type WiThrottle commands, verify log output
3. **Protocol test**: Send handshake (`N...\n`, `HU...\n`, `*+\n`), acquire loco (`MT+L3<;>L3\n`), set speed (`MTA*<;>V50\n`), verify responses and logs
4. **Heartbeat test**: Connect, enable heartbeat, stop sending -- verify e-stop logged after timeout
5. **Hardware test**: Configure LocoNet or Z21, connect WiFred device, verify loco responds
6. **mDNS test**: WiFred discovers server automatically without manual IP configuration
