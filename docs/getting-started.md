# wiFRED Server — Getting Started Guide

This guide explains how to set up the wiFRED Server so you can use your
wiFRED wireless throttles to drive trains at your club or at home.

No programming knowledge is needed. You only need to download one file,
change one setting, and start the program.

## What is the wiFRED Server?

The wiFRED Server is the "bridge" between your wiFRED throttle and your
command station. It translates the wireless signals from the wiFRED into
commands that your command station understands.

```
                          WiFi
    ┌─────────┐        ┌──────────┐        ┌─────────────────┐        ┌───────┐
    │ wiFRED  │ ~~~~~> │  WiFi    │ -----> │  wiFRED Server  │ -----> │ Command│
    │ throttle│        │  Router  │        │  (this program) │        │ Station│
    └─────────┘        └──────────┘        └─────────────────┘        └───────┘
                                                   │
                                            ┌──────────────┐
                                            │ Web Dashboard│
                                            │ (any browser)│
                                            └──────────────┘
```

```
                                                              ┌────────┐
                                         cable or             │Command │  track
┌────────┐  WiFi   ┌────────┐  WiFi   ┌────────┐  network    │Station │  wires   ┌───────┐
│ wiFRED │ · · · > │  WiFi  │ · · · > │ wiFRED │ ─────────> │(Z21 or │ ═══════> │ TRAIN │
│throttle│         │ Router │         │ Server │             │LocoNet)│          └───────┘
└────────┘         └────┬───┘         └────────┘             └────────┘
                        :
                        : WiFi
                        :
                   ┌────┴───┐
                   │ Phone/ │
                   │ Tablet │  Web Dashboard
                   └────────┘  (any browser)
```

## Why use wiFRED Server?

The wiFRED needs a WiThrottle server to connect to. There are other
WiThrottle servers available (for example JMRI, or the one built into
some newer command stations), so why choose this one?

- **Easy to set up** — Download one file, change one setting, and run.
  No Java installation, no complex software to configure.
  Works on Windows and Raspberry Pi.

- **Works with many command stations** — Supports Roco Z21 and
  Z21-compatible stations (TAMS mc2, YaMoRC 7001), as well as any
  command station with a LocoNet connection. You are not tied to one brand.

- **Web dashboard** — See all connected wiFREDs at a glance from any
  phone, tablet, or computer. Check battery levels, see which locos
  are being driven, and spot address conflicts — all without touching
  the wiFRED itself.

- **Change loco addresses remotely** — Click a loco address on the
  web dashboard to change it. The new address is sent directly to the
  wiFRED. Useful when preparing for a session or swapping locos.

- **Safety features** — If a wiFRED loses connection (e.g. out of WiFi
  range or battery dies), the server automatically stops all its trains.

- **Seamless network switching** — If your wiFRED has multiple WiFi
  networks configured (e.g. one for the club and one for home), the
  server automatically ensures the wiFRED connects properly to the
  right server each time. Other servers may leave you with a wiFRED
  that joins the WiFi but never connects to the server.

- **Runs on a Raspberry Pi** — Small, low-power, and can run headless
  (without a screen). Ideal for a permanent setup at the club.

## What You Need

Before you start, make sure you have these items:

### 1. A wiFRED throttle

The small wireless throttle with a speed knob and function buttons.
It must already be assembled and have firmware installed.
See the [wiFRED documentation](https://newheiko.github.io/wiFred/documentation/docu_en.html)
for assembly instructions.

### 2. A WiFi network

A wireless network that both the wiFRED and the computer running the
server can connect to. This is usually the WiFi router at your club or home.

**Important:** The wiFRED and the server computer must be on the **same**
WiFi network.

### 3. A computer to run the server

This can be:

- A **Windows PC** (Windows 10 or later) — a laptop works fine
- A **Raspberry Pi** (model 3 or later, 32-bit or 64-bit OS)

The computer must be connected to the same WiFi network as the wiFRED.

### 4. A command station

You need a command station that either speaks the Z21 network protocol
or has a LocoNet connection. Several command stations are supported:

- **Roco Z21** (or Z21 compatible, such as **TAMS mc2** or **YaMoRC 7001**)
- Any command station with a **LocoNet** port

> **Note:** Some command stations have a WiThrottle server built in
> (such as TAMS mc2 and newer YaMoRC models) and can work with wiFRED
> directly. You can still use this server for the web dashboard,
> remote address editing, and automatic network switching features.

The wiFRED server supports several ways to connect:

| Setup | What you need | Best for |
|-------|--------------|----------|
| **Z21 protocol** | Z21 or compatible, connected to router with a network cable | The server communicates directly with the command station over the network |
| **USB-to-LocoNet** | LocoBuffer-NG or similar USB-to-LocoNet adapter | Direct connection to LocoNet, not through the network |
| **LocoNet over TCP** | Software like Rocrail or LbServer already running | When you already use Rocrail or similar software |

```
Z21 / Network setup:

┌────────┐  WiFi   ┌────────┐  WiFi   ┌────────┐ network ┌────────┐  track  ┌───────┐
│ wiFRED │ · · · > │  WiFi  │ · · · > │ wiFRED │  cable  │Z21/TAMS│  wires  │       │
│throttle│         │ Router │         │ Server │ ──────> │mc2/7001│ ══════> │ TRAIN │
└────────┘         └────────┘         └────────┘         └────────┘         └───────┘


USB-to-LocoNet setup:

┌────────┐  WiFi   ┌────────┐  WiFi   ┌────────┐  USB    ┌────────┐LocoNet ┌────────┐ track  ┌───────┐
│ wiFRED │ · · · > │  WiFi  │ · · · > │ wiFRED │ cable   │ Loco-  │ cable  │Command │ wires  │       │
│throttle│         │ Router │         │ Server │ ──────> │ Buffer │ ─────> │Station │ ═════> │ TRAIN │
└────────┘         └────────┘         └────────┘         └────────┘        └────────┘        └───────┘


· · · >  = WiFi (wireless)
──────>  = cable (wired)
══════>  = track wires
```

## Step-by-Step Setup

### Step 1: Download the server

Go to the [Releases page](https://github.com/tellurianinteractive/Tellurian.Trains.WiThrottle/releases)
and download the zip file for your system:

| Your computer | Download this file |
|--------------|-------------------|
| Windows PC | `wifred-server-win-x64.zip` |
| Raspberry Pi (32-bit OS) | `wifred-server-linux-arm.zip` |
| Raspberry Pi (64-bit OS) | `wifred-server-linux-arm64.zip` |

> **Tip:** Not sure if your Raspberry Pi runs 32-bit or 64-bit?
> Open a terminal and type `uname -m`. If it says `armv7l`, download
> the 32-bit version. If it says `aarch64`, download the 64-bit version.

### Step 2: Unzip the files

Unzip the downloaded file into a folder. You can put it anywhere you like.

**Windows:** Right-click the zip file and choose "Extract All..."

**Raspberry Pi:** Open a terminal and type:
```
unzip wifred-server-linux-arm64.zip -d wifredserver
```

### Step 3: Tell the server which command station to use

Open the file `appsettings.json` in the program folder with a text editor.

Find the `"CommandStation"` section. You only need to change the `"Type"` line
to match your setup. Here are the three most common configurations:

#### If you have a Z21 or Z21-compatible command station

This applies to Roco Z21, TAMS mc2, YaMoRC 7001, and other command stations
that support the Z21 network protocol.

Change the type to `"Z21"` and set the IP address of your command station.
The Roco Z21 usually has the address `192.168.0.111` — check your Z21 app
or your command station's documentation if unsure.

The command station must be connected to your network router with a
**network cable** (the Z21 does not have WiFi).

```json
{
  "CommandStation": {
    "Type": "Z21",
    "Z21": {
      "Address": "192.168.0.111"
    }
  }
}
```

```
┌────────┐  WiFi   ┌────────┐ network ┌────────────────┐  track   ┌───────┐
│ wiFRED │ · · · > │  WiFi  │  cable  │ Z21 / TAMS mc2 │  wires   │       │
│throttle│         │ Router │ ──────> │ / YaMoRC 7001  │ ═══════> │ TRAIN │
└────────┘         └────┬───┘         │192.168.0.111   │          └───────┘
                        :             └────────────────┘
                        : WiFi
                   ┌────┴───┐
                   │ wiFRED │
                   │ Server │
                   └────────┘
```

#### If you have a USB-to-LocoNet adapter

Change the type to `"Serial"` and set the correct port name.

On **Windows**, the port is usually `COM3`, `COM4`, or similar.
You can find it in Device Manager under "Ports (COM & LPT)".

On **Raspberry Pi**, the port is usually `/dev/ttyUSB0`.

```json
{
  "CommandStation": {
    "Type": "Serial",
    "SerialPort": {
      "PortName": "COM3"
    }
  }
}
```

```
┌────────┐  WiFi   ┌────────┐  WiFi   ┌────────┐   USB    ┌────────┐ LocoNet ┌────────┐ track  ┌───────┐
│ wiFRED │ · · · > │  WiFi  │ · · · > │ wiFRED │  cable   │ Loco-  │  cable  │Command │ wires  │       │
│throttle│         │ Router │         │ Server │ ───────> │ Buffer │ ──────> │Station │ ═════> │ TRAIN │
└────────┘         └────────┘         └────────┘          └────────┘         └────────┘        └───────┘
                                      COM3 (Windows)
                                      /dev/ttyUSB0 (Linux)
```

#### If you use Rocrail or LbServer

This is for setups where you already have software running that provides
LocoNet access over the network (such as Rocrail or LbServer).

Change the type to `"LocoNetTcp"` and set the hostname of the computer
running the LocoNet server. If the server runs on the same computer,
use `"localhost"`.

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

> **Save the file** after making your changes.

### Step 4: Start the server

**Windows:** Double-click `Tellurian.Trains.WiFreds.exe` in the program folder.
Alternatively, open a command prompt in the program folder and type:
```
./Tellurian.Trains.WiFreds
```

**Raspberry Pi:** Open a terminal in the program folder and type:
```
chmod +x Tellurian.Trains.WiFreds
./Tellurian.Trains.WiFreds
```
(The first line is only needed the first time.)

You should see a message like this in the terminal/command prompt:

```
wiFRED Server listening on port 12090
mDNS service advertised: WiFred Server
```

The server is now running and waiting for wiFRED throttles to connect.

> **Tip:** Leave this window open. Closing it stops the server.

### Step 5: Connect your wiFRED

1. Make sure your wiFRED is configured with the same WiFi network
   as the server computer. See the
   [wiFRED manual](https://newheiko.github.io/wiFred/documentation/docu_en.html)
   for how to configure WiFi on the wiFRED.
2. Turn on the wiFRED.
3. Wait a few seconds — the wiFRED will automatically find and connect
   to the server.

The wiFRED finds the server automatically. You do not need to enter any
IP address or port number on the wiFRED.

### Step 6: Drive trains!

Once connected, use the wiFRED as normal:

- Turn the **speed knob** to control speed
- Use the **direction switch** to change direction
- Press the **function buttons** (F0 for lights, etc.)

## Using the Web Dashboard

The server includes a web page where you can see all connected wiFREDs.
Open a web browser on any device connected to the same network and go to:

```
http://<server-address>:5000
```

Replace `<server-address>` with the IP address of the computer running
the server. For example: `http://192.168.0.100:5000`

> **Tip:** If the server runs on the same computer, you can use
> `http://localhost:5000`

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│ Connected wiFREDs                                                                   │
│ Click a red loco address to change it.                                              │
│                                                                                     │
│ Name       │ IP Address    │ Battery │ Loco 1 │ Loco 2 │ Loco 3 │ Loco 4 │ Last Seen│
│────────────┼───────────────┼─────────┼────────┼────────┼────────┼────────┼──────────│
│ Stefan     │ 192.168.0.42  │  78%    │ ▓ 405  │ ▓ 406  │ ░  42  │ ░   -  │ 14:32:05 │
│ Heiko      │ 192.168.0.51  │  23%  ! │ ▓1234  │ ░   3  │ ░   -  │ ░   -  │ 14:31:58 │
│                                                                                     │
│ Last updated: 14:32:10                                                              │
│                                                                                     │
│  ▓ = green (loco in use)          ░ = red (idle — click to change address)          │
│                                   ! = low battery warning                           │
└─────────────────────────────────────────────────────────────────────────────────────┘
```

The dashboard shows:

- **Device name** — the name you gave your wiFRED
- **Battery level** — shown as a percentage; turns red when low
- **Loco addresses** — green means the loco is being driven, red means idle
- **Configure button** — opens the wiFRED's own settings page

You can **change a loco address** directly from the dashboard: click on a
red (idle) address, type the new address, and click Save. The change is
sent to the wiFRED immediately.

## Troubleshooting

### The wiFRED does not connect

- Check that the wiFRED and server are on the **same WiFi network**.
- Make sure only **one WiFi network** is enabled on the wiFRED.
  Having multiple networks enabled can prevent the connection.
  The server will fix this automatically if it can reach the wiFRED,
  but for the first connection they must be on the same network.
- Check that the server is running (you should see messages in the
  terminal window).

### The wiFRED connects but locos do not move

- Check that the command station is powered on and connected.
- Check that the `"Type"` setting in `appsettings.json` matches
  your command station setup.
- For Z21: check that the IP address in `appsettings.json` matches
  your Z21.
- For USB-to-LocoNet: check that the port name is correct.
  On Windows, look in Device Manager. On Raspberry Pi, try
  `ls /dev/ttyUSB*` in a terminal.

### The web dashboard does not load

- Make sure you are using the correct IP address and port (default 5000).
- Try `http://localhost:5000` if the browser is on the same computer.
- Check that your browser is up to date (Chrome, Firefox, Edge, or Safari).

### Locos show as red (idle) on the dashboard even though I am driving

- This can happen if the server restarted while the wiFRED was connected.
  Turn the wiFRED off and on again.
