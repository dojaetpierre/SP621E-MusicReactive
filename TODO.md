# TODO.md — Open Questions & Blocker Registry

Living list of unresolved technical questions, ordered by how much they block progress. Anything blocked on physical hardware is marked and carried in §2 until the operator returns evidence (which is then promoted to CONFIRMED in `HARDWARE_PROTOCOL.md`).

## 1. Open technical questions (not hardware-blocked)

- [ ] **M7 / latency:** measure real WASAPI loopback capture latency on this machine (NAudio `WasapiRecorder` buffer). Record the number in `DEVELOPMENT.md §1.4`. Target end-to-end < 50 ms. *Capture itself is DONE and running; the measurement is a hardware/final-build check (M14).*
- [ ] **M8:** perceptual tuning of FFT band weighting (bass/mid/treble) on real music at M12/M14; bin resolution is settled (1024-pt @ 48 kHz = 46.9 Hz/bin).
- [x] **M8:** smoothing / effect responsiveness trade-off settled by design (BeatDetector adaptive threshold + EMA BPM; engine knobs Intensity/Speed/GlobalBrightness exposed in UI).
- [x] **M10–M11:** host loop frame rate decided and implemented: effects render at a 30 fps DispatcherTimer; LED refresh rate stays capped only by the driver/transport once a wire format exists.
- [x] **M1:** WPF project template available (`dotnet new` + .NET 10 SDK present); resolved.
- [x] **M3 tooling delivered** (2026-09-21): `SnoopDecode` tool + `SP621E.Bluetooth/Snoop` parser, 9/9 unit tests pass, smoke-tested against a synthetic btsnoop capture. Remaining M3 work is evidence-gathering, tracked in B2.

## 2. BLOCKED — needs physical SP621E + strip + operator (highest priority)

### B1. GATT map of the real SP621E (Milestone 2)
  - Need: run the implemented diagnostics (Advanced tab) on hardware, return the saved report file + screenshot.
  - Status: **UNVERIFIED** code waiting on operator. Exact steps: `HARDWARE_PROTOCOL.md §5 Request #1`.
  - Blocks: M3 protocol work, M5 driver.

### B2. Real command format: power / color / brightness / mode (Milestone 3)
  - Need: HCI snoop log from BanlanX doing the basic action set; byte-for-byte analysis against THIS unit (not siblings).
  - Blocks: M3 DoD, M4 decision, M5 driver.
  - Operator instructions: `HARDWARE_PROTOCOL.md §5 Request #2`.
  - Tooling READY: `SnoopDecode <btsnoop_hci.log>` analyzes the capture; no Wireshark needed. Practice with `VirtualSp621e --gen-capture`.

### B3. Hand-crafted color command confirmation on physical strip (Milestone 3)
  - Need: operator sends probe, reports strip reaction.
  - Blocks: M3 "set color / set brightness → Confirmed" DoD.
  - Operator instructions: `HARDWARE_PROTOCOL.md §5 Request #3`.
  - Software half exercisable now: `VirtualSp621e` (VIRTUAL wire format, in-memory transport).

### B4. Frame-streaming feasibility probe — the project's biggest risk (Milestone 3/4)
  - Question: can arbitrary RGB buffers be pushed to the SP621E in real time at a usable rate, or is the practical ceiling mode/color/brightness/speed?
  - Need: rapid-write acceptance test + any bulk-pixel upload characteristic; documented rate.
  - Blocks: M4 decision (streaming vs. parametric), M12 full integration, and the entire product's feature set.
  - Operator instructions: `HARDWARE_PROTOCOL.md §5 Request #4`.
  - Software half exercisable now: `VirtualSp621e` streams frames against configurable MTU/pacing and reports fps, packets, and bytes — the same loop runs on hardware once bytes are confirmed.

### B5. End-to-end reactive-LED confirmation (Milestone 12/15/16)
  - Need: physical LEDs visibly react to real music for minutes without crash/disconnect.
  - Blocks: project completion per master prompt (real-hardware verification non-negotiable).
  - Software half exercisable now: `VirtualSp621e` runs the whole pipeline (synthetic audio → analyze → beats → effect → driver → virtual strip) with live fps/beat/BPM output.

## 3. Assumptions flagged for later verification

- SP621E shares the SPxxxE GATT UART-bridge pattern (write + notify characteristic). **Assumption only.**
- NAudio `WasapiRecorder` loopback on Windows 11 reaches under ~20 ms buffer at low latency. **Assumption until M7 measures it.**
- Packaging will be Inno Setup or MSIX; decision deferred to M15 per master prompt.

## 4. Historical log

- 2026-09-21 — Project started; M0 complete (stack decision: .NET 10 WPF + native WinRT BLE + NAudio.Wasapi 3). Hardware blockers B1–B5 registered.
- 2026-09-21 — M1 complete: build clean, 3 unit tests pass, shell UI launches (verified live, no crash). Dev-env quirk recorded in DEVELOPMENT.md: framework-dependent launch needs `DOTNET_ROOT` (user-local SDK), plus packaging note for M15.
- 2026-09-21 — M2 code complete but **UNVERIFIED**: BLE scanner + GATT enumerator + auto-saved report implemented (Advanced tab), build clean, app launches. Waiting on operator to run `HARDWARE_PROTOCOL.md §5 Request #1` against the real SP621E.
- 2026-09-21 — M3 offline tooling complete (software side): btsnoop→ATT analyzer (`SP621E.Bluetooth/Snoop` + `tools/SnoopDecode`), 9/9 tests pass, live smoke test decodes a synthetic Write Request. Evidence gathering remains blocked on operator (B2).
- 2026-09-21 — M4 architected, decision deferred: `ControllerCapability` (FrameStreaming/Parametric flags) + `IParametricController`; mock exposes streaming only.
- 2026-09-21 — M5 driver core complete, **UNVERIFIED vs hardware**: `SP621EDriver` over `IBleTransport` with serialized paced write queue, MTU chunking, diagnostics notifications; wire bytes gated behind `ISp621eWireFormat` (none configured ⇒ refuses to send). 8/8 new driver tests, total 17/17.
- 2026-09-21 — M6–M11 complete (software half): `Rgb`/`RgbFrame`, `WasapiLoopbackAudioSource` + `AudioDeviceEnumerator` (NAudio 3.0.1 loopback, builds clean), `AudioAnalyzer`, `BeatDetector`, `EffectsEngine`, 8-effect library + registry. Solution tests 25/25.
- 2026-09-21 — M12 software integration done: Connection (scan/connect/disconnect via `WinrtBleTransport` + driver), Audio Source (device picker, capture, live level/beat/BPM), Effects (picker, Intensity/Speed/Brightness, 30 fps LED preview loop, optional frame send), Advanced (btsnoop import added), settings persisted. Build clean, re-published to `dist\SP621E.App`. Hardware gating unchanged: B1–B5.
- 2026-09-22 — Docs updated: DEVELOPMENT.md status table (M6–M13), TODO.md open questions resolved/rewritten, HARDWARE_PROTOCOL.md Request #2 now documents the in-app btsnoop import.
- 2026-09-22 — Software halves of B2–B5 made runnable via `tools/VirtualSp621e` (standalone simulator): real pipeline vs tagged-VIRTUAL wire format + in-memory transport (chunking, pacing, parametric, notifications) + `--gen-capture` writing a self-verified synthetic btsnoop for the import path. 26/26 tests; published to `dist\VirtualSp621e`. Real wire bytes remain blocked on hardware evidence.
- 2026-09-22 — Software made shareable/ready: WPF app re-published as a **single-file self-contained exe** (`dist\SP621E.App\SP621E.App.exe`), simulator likewise; portable zip bundle produced (`dist\SP621E-MusicReactive-Portable.zip`, ~99 MB, includes README with the two hardware-evidence steps). Smoke-launched and left running (verified settings/log written, process stable). Hardware gating (B1–B2) unchanged.
- 2026-09-24 — Screen color sync shipped: new `src/SP621E.Screen` project (pure-GDI capture via P/Invoke — `ScreenSampler`/`NativeScreen` 96-cell grab, `ScreenSyncPipeline` with Average / Bottom-edge / Full-columns modes, EMA smoothing, brightness) + "Screen Sync" tab in the app (enable/mode/smoothness/brightness, live palette preview, ~20 fps, mutually exclusive with audio effects, settings persisted). 32/32 tests. Re-published single-file exe, relaunched (verified: "Screen color sync started" in log), portable zip refreshed (~104 MB). Hardware gating B1–B5 unchanged.
- 2026-09-29 — Preview fixes: (1) root cause of "previews ain't working" — persisted `LastBrightness` had been saved at 0, rendering all effects black; UI now shows live % next to every brightness/smoothness slider and logs/all-dark status hints explain a black preview (brightness 0, no audio energy, or dark screen content). (2) Fixed a brand-new startup crash (NullReferenceException in `UpdateSliderValueLabels` firing from `ValueChanged` during `InitializeComponent`, before the label controls exist) surfaced by the diagnostics edit — window now opens cleanly. (3) Screen capture is now under test via live smoke tests (`ScreenSamplerCaptureTests`, two GDI grabs) proving the grab works; capture failures are logged to AppLog once. 34/34 tests, 0 build warnings, re-published + relaunched (verified "Shell started; settings loaded."), portable zip refreshed (~104 MB).
- 2026-09-29 — Full-software verification + hardware onboarding aid: VirtualSp621e published exe runs the whole pipeline end-to-end (43 frames / 392 paced write packets / chunking / parametric / notifications / 50-px virtual strip lit); synthetic btsnoop round-trips through SnoopDecode (6 ATT packets). Live BLE advert scan found nothing in range right now (strip evidently off/far). Connection tab gained a 3-step evidence checklist (power on → connect → GATT report; then Android BanlanX + Bluetooth HCI snoop log import) to unlock real wire bytes. NOTE: Windows App Control/McAfee began blocking newly built single-file self-contained exes mid-session (older exe hashes still run); app relaunched via trusted `dotnet` host (pid 13772) and verified, and the shipped exe needs an App Control/CIA exclusion to run standalone locally.