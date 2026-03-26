# WiThrottle server connection fails when multiple WiFi networks are enabled

## Observed behavior

When the wiFRED has two WiFi networks configured and enabled (e.g. a home network and a club network), it connects to WiFi successfully but fails to establish a TCP connection to the WiThrottle server. The device is visible on the network (responds to HTTP config requests and sends UDP broadcasts), but never opens the WiThrottle TCP session.

Disabling all networks except the one currently in use and restarting the wiFRED immediately resolves the issue.

## Steps to reproduce

1. Configure two WiFi networks on the wiFRED (e.g. "ClubNetwork" and "HomeNetwork"), both enabled
2. Run a WiThrottle server on each network, both advertising via mDNS (`_withrottle._tcp`)
3. Power on the wiFRED at the club — it connects to "ClubNetwork" and discovers the club server via mDNS (works fine)
4. Later, power on the wiFRED at home where only "HomeNetwork" is in range
5. The wiFRED connects to "HomeNetwork" WiFi but does not connect to the home WiThrottle server

The device appears on the network (responds to HTTP, sends UDP broadcasts) but never opens the WiThrottle TCP session. The cached server address from "ClubNetwork" is likely being reused.

## Expected behavior

The wiFRED should discover and connect to the WiThrottle server on whichever network it is currently connected to, regardless of how many networks are configured.

## Suspected cause

Looking at the firmware source, `MDNS.queryService("withrottle", "tcp")` is called after WiFi connects, and the result is cached in `automaticServer` / `automaticServerIP`. This cache does not appear to be cleared when the WiFi network changes. If the device previously resolved a server on a different network, the stale cached address may be used instead of running a fresh mDNS query.

## Environment

- wiFRED firmware: `2022-10-16-71ca8c3-master`
- Server: custom WiThrottle server advertising `_withrottle._tcp` via mDNS
- Server discovery: automatic (mDNS)

## Workaround

Enable only the WiFi network you are currently using. Disable any other configured networks and restart the wiFRED.
