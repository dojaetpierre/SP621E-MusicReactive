# SP621E Music-Reactive LED Controller

A Windows desktop app that captures your speakers' audio (WASAPI loopback), analyzes it in real time
(loudness, bass/mid/treble, beats, BPM), and drives a BLE LED strip (SP621E / BanlanX device). WPF,
.NET 10, self-contained, no installation.

## Downloads

Grab the portable build from the [Releases](https://github.com/dojaetpierre/SP621E-MusicReactive/releases)
page: one exe, no runtime needed (~104 MB, includes the no-hardware simulator).

## Status

- **Fully working today, no hardware required:** audio loopback capture, live level/beat/BPM meters,
  8 music-reactive effects with a 30 fps 50-LED preview, screen color sync, btsnoop HCI capture import,
  and the `VirtualSp621e` simulator that exercises the entire pipeline.
- **Physical strip:** live frame sending is deliberately gated on confirming the wire format from real
  hardware (the project never guesses protocol bytes). Two evidence files unlock it — the strip's GATT
  report plus a BanlanX HCI snoop log. See `HARDWARE_PROTOCOL.md` section 5 and the in-app checklist on
  the Connection tab.

## Building / testing

```powershell
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"

dotnet build SP621E-MusicReactive.slnx
dotnet test  SP621E-MusicReactive.slnx

# Published single-file app:
dotnet publish src\SP621E.App -c Release -r win-x64 --self-contained true -o dist\SP621E.App
```

## Layout

- `src/SP621E.Core` — engine, effects, analysis, configuration (hardware-free)
- `src/SP621E.Audio`  — WASAPI loopback capture + device enumeration
- `src/SP621E.Bluetooth` — BLE transport, driver, btsnoop/GATT forensics
- `src/SP621E.Screen` — GDI screen grabbing and the sync pipeline
- `src/SP621E.App` — WPF shell
- `tools/VirtualSp621e` — no-hardware simulator
- `tests/` — 34 unit/smoke tests

## Notes for recipients

- The exe is unsigned; Windows SmartScreen may warn ("More info -> Run anyway").
- Requires Windows 10/11 and a Bluetooth 4.0+ radio for the strip.
- Settings/logs/diagnostics live only under `%LocalAppData%\SP621EMusicReactive\`; no telemetry.