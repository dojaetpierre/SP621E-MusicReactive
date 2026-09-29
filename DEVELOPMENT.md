# DEVELOPMENT.md — SP621E Music-Reactive LED Controller

Programmer-facing development ledger. Created at Milestone 0 and updated continuously.

**Document conventions (mandatory):** every claim below is tagged as one of:

- **CONFIRMED** — verified by direct evidence (real captured traffic, physical hardware test, running code).
- **ASSUMPTION** — believed true but not yet verified; must never be treated as fact in code or UI.
- **UNVERIFIED** — code that cannot be tested in this environment (almost always: anything touching real SP621E hardware, which is unavailable here). Flagged until a human operator runs the prescribed test and returns the evidence.

---

## 0. Project status

| Milestone | Status | Notes |
|---|---|---|
| 0 — Requirements & Stack Decision | **CONFIRMED / DONE** | This file. |
| 1 — Project Skeleton & UI Shell | **DONE** | Builds clean, tests pass, shell launches (verified 2026-09-21). |
| 2 — BLE Discovery & SP621E Diagnostics | **DONE (code) / UNVERIFIED** | Diagnostic tooling implemented; needs operator run against real SP621E. |
| 3 — Protocol Investigation | **DONE (offline tooling) / UNVERIFIED** | btsnoop→ATT analyzer built + unit-tested (9/9 pass); capture still runs on hardware. |
| 4 — Frame Streaming vs Parametric Decision | **ARCHITECTED / DECISION-DEFERRED** | Both paths supported by the seam (capability flags + parametric interface); final choice awaits M3 evidence. |
| 5 — SP621E Driver | **DONE (core) / UNVERIFIED** | Protocol-agnostic driver over `IBleTransport` + evidence-gated `ISp621eWireFormat`; 8/8 unit tests; byte format requires hardware. |
| 6 — Generic LED Frame Abstraction | **DONE** | `Rgb` + `RgbFrame` (construction, uniform fill, map, timestamp), unit-tested. |
| 7 — Audio Capture | **DONE (code)** | `WasapiLoopbackAudioSource` (NAudio WasapiRecorder loopback, 60 ms buffer, event sync) + `AudioDeviceEnumerator`; compiled clean. Latency measured on hardware at M12/M14. |
| 8 — Real-Time Audio Analysis | **DONE** | `AudioAnalyzer` (Hann-windowed radix-2 FFT 1024, RMS, bass/mid/treble band energy, onset strength), unit-tested. |
| 9 — Beat & BPM Detection | **DONE** | `BeatDetector` (adaptive threshold, refractory + smoothed BPM), unit-tested. |
| 10 — Effects Engine Core | **DONE** | `EffectsEngine` (intensity/speed/brightness, active effect, PixelCount), unit-tested. |
| 11 — Initial Effect Library | **DONE** | 8 effects (Solid, BassPulse, Strobe, Spectrum, RainbowWave, Breathe, BeatSnap, Runner) via `EffectRegistry`. |
| 12 — Full Integration | **WIRED (software) / UNVERIFIED (hardware)** | WPF UI fully wired: BLE scan/connect/disconnect, loopback capture + live meter, effect picker + preview loop; frame streaming to the driver gated on wire-format evidence. |
| 13 — Polished UI | **WIRED** | Connection / Audio Source / Effects / Advanced (incl. btsnoop import) / Log tabs functional; persistence via `SettingsStore`. |
| 14 — Testing & Performance | pending | |
| 15 — Windows Installer | pending | |
| 16 — Documentation | pending | |

**Hardware availability — blocker record:** this development environment is a Windows 11 (build 26200) machine. **CONFIRMED** (from machine): it has no .NET SDK pre-installed (installed by us), no git, no Python. Whether the human operator can/did attach an SP621E + WS2811/SP6812 strip + run BanlanX + capture HCI logs is **UNKNOWN / not yet exercised** — nothing in this project claims real-hardware evidence until it exists.

---

## 1. Milestone 0 — Stack decision (and why)

### 1.1 What the environment looks like (surveyed 2026-09-21)

- Windows 11 Home, 64-bit, build 26200. **CONFIRMED** (local machine query).
- Network access to dot.net / nuget.org: **CONFIRMED** (TCP 443 reachable).
- `winget` present; Node.js v24 present; **no dotnet, no git, no Python** initially. **CONFIRMED**.

### 1.2 Candidates considered

**Option A — C#/.NET (WPF) with native Windows stacks (chosen).**
Runtime `net10.0-windows10.0.19041.0`, UI **WPF**, audio **NAudio.Wasapi 3.x** (`WasapiRecorder` builder with `WithLoopbackCapture()`), BLE **native `Windows.Devices.Bluetooth` / `Windows.Devices.Bluetooth.GenericAttributeProfile`** (no third-party BLE wrapper).

- **Windows support:** A+. First-class desktop citizen; WPF mature, no packaging constraints.
- **BLE maturity:** **CONFIRMED** — `Windows.Devices.Bluetooth` is a Microsoft-maintained, non-deprecated WinRT API explicitly usable from classic desktop apps (DualApiPartitioned). Reaches it via the `-windows10.0.xxxx` TFM (see §1.3). Community guidance (`techbloat` BLE roundup, 2026-05) and Microsoft docs both recommend the native APIs over wrappers (`InTheHand.BluetoothLE`, `Plugin.BLE`) for reliability + diagnostics when a Windows-only target is acceptable.
- **Low-latency audio:** **CONFIRMED library-wise** — NAudio 3.0.1 (2026-08-18) actively maintained; `WasapiRecorder` offers `IAudioClient3` low-latency mode, MMCSS thread priority, zero-copy `ReadOnlySpan` callbacks, and loopback capture of the system mixer. Actual loopback latency on this machine will be **measured and recorded** at Milestone 7 (target end-to-end < 50 ms; see §1.4).
- **Packaging ease:** A. MSIX or Inno Setup both well-trodden; self-contained single exe deployable.
- **UI polish:** A. WPF is the standard for this class of desktop tool.

**Option B — Node.js / Electron with `ble-winrt`, WASAPI loopback via Node package, FFT in worker threads.**
- **Windows support:** A-, but Electron ships ~150 MB+ Chromium per app and is heavier to keep current.
- **BLE maturity:** B+. `ble-winrt` is a maintained wrapper over the same WinRT APIs, but adds a layer between us and raw GATT for diagnostics; thinner ecosystem for low-level control.
- **Low-latency audio:** B. WASAPI-loopback Node packages are far less battle-tested than NAudio's; GC pauses and worker-thread marshalling add latency/jitter risk for a real-time pipeline.
- **Packaging:** B+.
- **UI polish:** B+.

**Option C — Python + `bleak` + PyAudioLoopback/WASAPI.** Python is not installed in this environment; packaging a polished commercial desktop app from Python is fiddlier; real-time latency characteristics weaker than NAudio loopback. Not competitive here.

### 1.3 Deviations from the master prompt's defaults (with reasons)

1. **.NET 10 instead of .NET 8.** **CONFIRMED** (Microsoft lifecycle page, 2026-09): .NET 8 (LTS) reaches end of support **2026-11-10** — six weeks after this project starts; .NET 9 (STS) the same day. .NET 10 (LTS, current) is supported to **2028-11-14**. Shipping a commercial product on a runtime entering EOL during the project is indefensible. Chosen: **.NET 10 SDK 10.0.401** (installed locally, CONFIRMED).
2. **WPF over WinUI 3.** WinUI 3 is fine but still carries more packaging/XAML friction for a plain desktop utility; WPF has the most mature data-binding + real-time visualization ecosystem and simpler MSIX/Inno packaging. Revisit at Milestone 13/15 only if the visual spec demands it.
3. **Native WinRT BLE rather than a NuGet BLE wrapper.** For a product whose Milestone 2/3 work is *protocol forensics*, we want the rawest possible view of GATT attributes, MTU, and write behavior. A wrapper that hides that is a liability. (.NET 5+ removed direct WinMD consumption; the supported path is the project's TFM carrying the Windows SDK projection.)
4. **JSON settings file in `%LocalAppData%`** instead of the registry — simple, diffable, corruptable-but-recoverable, easy to ship default presets.
5. **Inno Setup (or MSIX) deferred to Milestone 15** — exactly one decision, made against the then-current toolchain state, not pre-committed.

### 1.4 Target latency budget (to be re-measured against real hardware)

The prompt's end-to-end goal is < 50 ms audio→LED. Budget assumption (must be validated at M7, M12, M14):

| Stage | Assumed budget |
|---|---|
| WASAPI loopback buffer | ≤ 20 ms (IAudioClient3 minimum period, MMCSS) |
| FFT / analysis | ≤ 5 ms compute |
| Effects render | ≤ 5 ms compute |
| BLE transmit + controller latency | device-dependent (measured at M3/M5) |

### 1.5 Definition of Done — Milestone 0

- [x] Stack comparison performed: at least two options against (Windows support, BLE maturity, low-latency audio, packaging ease, UI polish).
- [x] BLE library maintenance **CONFIRMED** (native WinRT APIs; not a deprecating library).
- [x] WASAPI loopback capability **CONFIRMED library-wise**; on-machine latency to be measured (M7) and recorded here.
- [x] `DEVELOPMENT.md` exists with justified stack choice.
- [x] `HARDWARE_PROTOCOL.md` and `TODO.md` created (living docs requirement).

---

## 2. Architecture (from master prompt, restated as implemented)

```
Audio Input (WASAPI loopback)
   → Audio Analyzer (FFT, band energy, RMS)
   → AudioData (structured, timestamped feature frame)
   → Beat/BPM Detector
   → Effects Engine (pure function: AudioData + params → RGBFrame)
   → RGBFrame (generic pixel buffer, controller-agnostic)
   → Controller Driver interface (ILedController)
       → SP621EDriver (BLE, real hardware)     [UNVERIFIED until hardware]
       → MockLedController (testing/dev)
   → SP621E hardware → LED strip
```

Module boundaries (namespaces / projects, one per concern):

1. `Bluetooth/` — discovery, GATT interaction, connection lifecycle, reconnection. No audio/effects knowledge.
2. `Controllers/` — `ILedController` + `SP621EDriver` + `MockLedController`; converts `RGBFrame` → wire format.
3. `LedFrame/` — `RGBFrame` (RGB triples + pixel count + timestamp); depends on nothing.
4. `Audio/Capture/` — WASAPI loopback capture + device enumeration/selection.
5. `Audio/Analysis/` — FFT, band energy (bass/mid/treble), RMS, onset strength, smoothing.
6. `Audio/BeatDetection/` — beat events, BPM estimate.
7. `Effects/` — `IEffect`, parameterized effect impls, registry.
8. `Effects/Presets/` — saved parameter sets.
9. `AppState/` — connection status, active effect, running/stopped.
10. `UI/` — main window, connection panel, audio source picker, effect picker + params, visualizer, advanced/debug panel.
11. `Configuration/` — persisted settings (JSON).
12. `Logging/` — structured logging, rotated files, in-app log viewer (advanced panel).
13. `SP621E.Bluetooth/Snoop/` + `tools/SnoopDecode/` — offline btsnoop→ATT analyzer for M3 protocol forensics (pure C#, no hardware).
14. `SP621E.Bluetooth/Transport/` — `IBleTransport` seam: real WinRT impl arrives at M12 once GATT is mapped; driver tested against fakes.
15. `SP621E.Bluetooth/Wire/` — `ISp621eWireFormat`: the ONLY place device bytes exist, evidence-gated per M3; null ⇒ driver refuses to send (Capabilities = None).

**Invariant:** the audio + effects pipeline must run and be fully testable with synthetic audio and `MockLedController`, with zero hardware.

---

## 3. Key decisions log

| Date | Decision | Status | Rationale |
|---|---|---|---|
| 2026-09-21 | .NET 10 (LTS) + WPF, native WinRT BLE, NAudio.Wasapi 3 for loopback | CONFIRMED (build) | §1.2 / §1.3 |
| 2026-09-21 | Effects engine consumes `AudioData` and emits `RGBFrame`; driver reduces abstractly to wire format | ASSUMPTION (design) | Keeps engine hardware-agnostic under either M4 path |
| 2026-09-21 | SP621E protocol = fully unknown; will be reverse-engineered from live HCI capture in M3, never guessed or ported from SP107E/SP108E/SP110E docs | CONFIRMED (project rule) | Hard constraint from master prompt |
| 2026-09-21 | M3 offline analyzer (`SnoopDecode` + `SP621E.Bluetooth/Snoop`) — `net10.0-windows` console tool; parse validated against synthetic btsnoop files | CONFIRMED (9/9 unit tests) | Operator does capture; App-grade parse confidence without hardware |
| 2026-09-21 | M4: support BOTH control paths (streaming + parametric) via `ControllerCapability` flags and `IParametricController`; final choice deferred to evidence | CONFIRMED (design) | Don't pre-commit to a hardware decision; engine stays frame-based and hardware-free either way |
| 2026-09-21 | M5: `SP621EDriver` is protocol-agnostic; device bytes only via `ISp621eWireFormat` (null ⇒ `Capabilities=None`, commands throw with evidence pointer) | CONFIRMED (8/8 unit tests) | "Never guess protocol bytes" enforced in code, not just docs |
| 2026-09-21 | M6–M11 software layers shipped: `Rgb`/`RgbFrame`, `WasapiLoopbackAudioSource` (NAudio 3.0.1 WasapiRecorder loopback), `AudioAnalyzer`, `BeatDetector`, `EffectsEngine`, 8-effect library — all unit-tested | CONFIRMED (build + tests) | Hardware-free pipeline per architecture invariant |
| 2026-09-21 | WPF UI fully wired (M12 software half): scan/connect/disconnect via `WinrtBleTransport`, loopback capture + live meter/BPM, effect picker + 30 fps preview, SettingsStore persistence, btsnoop import in Advanced | CONFIRMED (build + tests) | End-user loop reachable without hardware; only the device bytes stay gated on evidence |
| 2026-09-22 | `tools/VirtualSp621e`: standalone simulator (user-chosen form of the hardware blockers). Runs the FULL pipeline against a tagged-VIRTUAL wire format + in-memory transport: power/color/brightness/effect, frame streaming with real MTU chunking + pacing, synthetic beat-driven audio → analyzer → beats → effects → ASCII strip. Also `--gen-capture` writes + self-verifies a synthetic btsnoop for the import path. | CONFIRMED (build, 26/26 tests, smoke run) | Software halves of B2–B5 runnable now; real wire bytes still blocked on §5 evidence |
| 2026-09-24 | Screen color sync (`src/SP621E.Screen` + app "Screen Sync" tab): pure-GDI desktop grab (user32/gdi32 P/Invoke, no System.Drawing/NuGet) downscaled to a 96-cell grid, `ScreenSyncPipeline` maps Average / Bottom-edge / Full-columns to LED frames with per-LED EMA smoothing + brightness; 50 ms background capture loop in the app, mutually exclusive with the audio-effects render loop, persists enable/mode/smoothness/brightness in settings. Device frames still flow through the same evidence-gated driver (no new wire bytes invented). | CONFIRMED (build 0 warnings, 32/32 tests incl. 6 pipeline tests, published + relaunched live) | Adds a second, hardware-agnostic input source; hardware gating B1–B5 unchanged |
| 2026-09-29 | Preview-robustness + hardware checklist: brightness/smoothness sliders now show live %; all-black previews produce self-explanatory status text (brightness 0, no audio energy, dark screen content); screen-capture failures log to AppLog once; 2 live GDI capture smoke tests (34/34). Connection tab gained a three-step "unlock frame sending" checklist (B1 GATT report, B2 BanlanX HCI capture). | CONFIRMED (build 0 warnings, 34/34 tests, launched via dotnet host) | NOTE: App Control policy began blocking newly built single-file self-contained exes (learn/lockdown; older exes still run). Full-software runs signed off; exe needs an App Control/McAfee approval or exclusion to launch locally. |
| 2026-09-29 | Public internet distribution: repo + v0.9.0 release live at https://github.com/dojaetpierre/SP621E-MusicReactive (public), portable zip (~104 MB) attached as release asset with SHA-256 in notes. Landing page `dist/web/index.html` + `dist/HOSTING.md` prepared. Git 2.55 + gh 2.101 installed via winget; repo initialized `main`, single commit. | CONFIRMED (release published, asset verified on the release page) | Software is fully verifiable without hardware (simulator), so a public build is meaningful while B1–B5 stay evidence-gated. exe unsigned → SmartScreen note in README/notes. Gating unchanged: real frame bytes ship only after §5 evidence. |

---

## 4. How to build / run / test (kept current at all times)

```powershell
# one-time (already done in this environment):
#   .NET 10 SDK 10.0.401 installed to $env:LOCALAPPDATA\Microsoft\dotnet
#   .NET 10 Desktop Runtime 10.0.12 installed to the same location
# NOTE: SDK+runtime live OUTSIDE C:\Program Files\dotnet, so dev launches need DOTNET_ROOT
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"

dotnet build SP621E-MusicReactive.slnx
dotnet test  SP621E-MusicReactive.slnx
dotnet run --project src/SP621E.App
dotnet run --project tools/SnoopDecode -- <btsnoop_hci.log>   # M3 protocol forensics
dotnet run --project tools/VirtualSp621e -- --seconds 10       # software drill (B2–B5)

# Synthetic btsnoop sample for the app's Advanced-tab import (or SnoopDecode):
dotnet run --project tools/VirtualSp621e -- --gen-capture evidence\sample-btsnoop.btsnoop

# Double-click build (self-contained, no runtime needed):
dotnet publish src/SP621E.App -c Release -r win-x64 --self-contained true -o dist\SP621E.App
Launch-SP621E-App.cmd   # starts dist\SP621E.App\SP621E.App.exe

# Single-file shareable build (WPF app + optional simulator):
dotnet publish src/SP621E.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\SP621E.App
dotnet publish tools/VirtualSp621e -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist\VirtualSp621e
# then copy SP621E.App.exe + VirtualSp621e.exe + dist\README-PORTABLE.txt into a zip
# (current artifact: dist\SP621E-MusicReactive-Portable.zip, ~104 MB)
```

**Packaging note (recorded for M15):** because the runtime is user-local here, framework-dependent
launch requires `DOTNET_ROOT` pointing at `%LOCALAPPDATA%\Microsoft\dotnet` (this was hit as
`HOST_E_CLRNOTAVAILABLE` / 0x80008083 on first launch at M1). The commercial installer must either
deploy self-contained, or install the .NET Desktop Runtime machine-wide so end users never need this.

Living record: update as projects and scripts materialize in M1.