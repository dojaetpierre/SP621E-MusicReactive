# HARDWARE_PROTOCOL.md — SP621E Bluetooth LE Protocol Record

The **only** authoritative record of confirmed SP621E behavior. Nothing is added to the sections marked **CONFIRMED** without direct evidence: captured HCI traffic, or a command demoed on the physical device and visually confirmed by a human operator.

Status vocabulary (identical to DEVELOPMENT.md):
- **CONFIRMED** — real capture and/or tested on physical hardware.
- **ASSUMED / UNCONFIRMED** — hypothesis, lead, or ported-from-a-sibling-device note. Never used to write production code.
- **BLOCKED** — investigated up to a wall that requires physical hardware; operator action needed.

---

## 1. Device overview

- Target: **SP621E** Bluetooth LED controller (drives WS2811/WS2812B/SP6812-family addressable strips), normally used with the **BanlanX** app.
- **CONFIRMED (device identity):** none yet — we have no capture from the real unit.
- **ASSUMED (for investigation only):** SP621E belongs to the SPxxxE vendor family (SP107E/SP108E/SP110E et al.), where the common sibling pattern is a GATT UART-bridge (one write characteristic, one notify characteristic) with short fixed-format command frames (power/color/brightness/mode/speed). **This is a lead to test, not a spec. Sibling integrations (`led-ble`, `flux_led`, SP110E-HASS) exist but their command tables are NOT trusted for this unit.**

## 2. Evidence inventory

| # | Evidence | Source | Status |
|---|---|---|---|
| — | *(none yet)* | — | — |

Every capture/log dump referenced below will be recorded in `evidence/` with a filename, date, and the operator who produced it (see §5 operator instructions).

## 3. GATT map

*To be filled from Milestone 2 diagnostic tool output against the real controller.* Then this becomes the canonical map:

| Service UUID | Characteristic UUID | Properties (R/W/N/I) | Purpose (if known) |
|---|---|---|---|
| — | — | — | — |

## 4. Commands

### 4.1 Confirmed commands
None yet. **Everything below the master prompt's Milestone 3 requires operator capture.**

### 4.2 Command table (ASSUMED, sibling-derived — DO NOT implement from this)
*Placeholder reserved for the SP107E/SP110E "hypothesis generator" table, clearly labeled, for the operator's M3 capture work. Only captured bytes for this specific SP621E get promoted to §4.1.*

## 5. Operator instructions (performed on a machine with the real hardware)

Blocked items need these steps actioned and the outputs returned. The most current open requests always live in `TODO.md`.

### Request #1 — Milestone 2: GATT enumeration (needed to unblock M3)
Status: **BLOCKED — awaiting operator.**

The diagnostic tool is implemented (Advanced → Scan for BLE devices, then Connect & enumerate GATT). It auto-saves a full report to
`%LocalAppData%\SP621EMusicReactive\diagnostics\gatt-report-<timestamp>.txt`. Status: **UNVERIFIED** until exercised.

1. Have the SP621E powered and in range (strip attached).
2. Launch the app → **Advanced** tab.
3. Click **"Scan for BLE devices"**; wait until the SP621E appears in the list (shows name + MAC + RSSI).
4. **Stop** the scan, **select the SP621E** in the list.
5. Click **"Connect & enumerate GATT services"** and wait for the report to complete (it auto-saves).
6. Return to the developer:
   - the saved file path shown next to "Save report", AND
   - a screenshot/paste of the GATT report panel, AND
   - the on-screen status line.
7. If the scan finds nothing, also report: does this machine have Bluetooth at all (Settings → Bluetooth)? What other BLE devices appear, if any?

### Request #2 — Milestone 3: HCI packet capture while using BanlanX
Status: **BLOCKED — awaiting operator.**

An offline analyzer (`SnoopDecode`) exists and is unit-tested; it extracts the ATT
write/notify payloads from the capture so no Wireshark expertise is needed.

**Capture (on Android):**
1. Android 11+: `Settings → Developer options → Bluetooth HCI snoop log` → enable.
2. Put the SP621E in range with the strip attached; start BanlanX and connect to it.
3. Perform each action slowly, ~5 s apart, so snapshots are separable:
   1. Power on.
   2. Set solid color: red.
   3. Set solid color: green.
   4. Set brightness low → high.
   5. Select 3–4 different built-in effects.
4. Disable snooping. Export the log: `Settings → Developer options → Bluetooth
   HCI snoop log` panel shows `Share system HCI log` (saves over Bluetooth) or use
   `adb pull /sdcard/btsnoop_hci.log` (a rooted/ADB-connected phone). The file may be
   `btsnoop_hci.log` or `btsnoop_hci.cfa` — either works.

**Analyze (no Wireshark needed) — either route:**
- **In-app (easiest):** launch the app → **Advanced** tab → **"Import btsnoop capture…"**, select the exported log. The ATT packet stream is decoded, displayed, and auto-saved to `%LocalAppData%\SP621EMusicReactive\diagnostics\btsnoop-decode-<timestamp>.txt`.
- **CLI:** 
```powershell
cd SP621E-MusicReactive
dotnet publish tools/SnoopDecode -c Release -o dist/snoopdecode
dist\snoopdecode\SnoopDecode.exe C:\path\to\btsnoop_hci.log
```
Return to the developer: the decoded output (console or saved file), plus the original capture file if possible.

**No-capture practice mode:** `tools/VirtualSp621e --gen-capture <path>` writes a
structurally valid *synthetic* btsnoop (clearly not SP621E evidence) and verifies it
round-trips; use it to rehearse the import path end-to-end before the real capture
exists. The same tool runs the software halves of B2–B5 with a VIRTUAL wire format.

### Request #3 — Milestone 3: hand-crafted "set color" probe
After §2 GATT map is known, the developer ships a small test app/probe. Operator: select the target characteristic, send the probe payload, and report **exactly what the strip does** (color reached / wrong color / no change / controller reset).

### Request #4 — Milestone 3: frame-streaming feasibility probe
Whether rapid sequential writes are accepted and at what rate, and any bulk-pixel characteristic. Verdict feeds the M4 decision.

## 6. Streaming-vs-parametric verdict (Milestone 4)
**NOT YET DECIDED — blocked on Milestone 3 evidence.** Recorded here the moment the decision is made, with reasoning.

## 7. Firmware / unit quirks log
*(to record e.g. MTU negotiation results, cached-service behavior, power-on races, reconnection latencies as they are observed)*