# Running it on a TV

A TV's own OS (Android / Google TV / Roku / webOS) can never run this app: it is a Windows
WPF program, and TV platforms give apps no access to the rest of the system's audio, which is
what the whole analysis needs. So the app lives on a small Windows device wired to the TV.
Two setups cover everything.

## Setup A — the Windows box is also the player (simplest, zero extra hardware)

```
[ Windows mini-PC / HDMI stick (has Bluetooth) ] --HDMI--> [ TV ]
        |  BLE
        v
 [ LED strip ]
```

- Play the content ON the box: browser (Netflix/YouTube/Plex), a media player, or a game.
- Audio Source tab -> "Computer sound (loopback)" (the default) -> Start capture.
- Screen Sync tab -> Enable -> "Average (whole screen)" -> the LEDs follow what is on the TV.
- Nothing extra to buy. This already worked before this release.

## Setup B — watch from a console / Roku / Chromecast, strip follows the TV

```
[ console / Roku / ... ] --HDMI--> [ USB HDMI capture card ] --USB--> [ Windows box ] --HDMI--> [ TV ]
                                                                       BLE
                                                                        v
                                                                     [ LED strip ]
```

The capture card's audio arrives on the Windows box as an **input device**, which is what
v0.9.1 added support for:

- Audio Source tab -> choose "Input device (mic / line-in / TV audio)" -> Refresh -> pick the
  capture card (e.g. "HDMI Capture", "USB Audio") -> Start capture.
- The strip now beats to whatever the TV is playing, even though the box plays nothing.
- Screen Sync tab -> Enable -> "Average (whole screen)": the capture card echoes the HDMI feed
  to the box's display, so the screen-sync makes the LEDs track the TV picture too.
- Want both TV-reactive color AND audio? Run Audio Source (input) and Screen Sync at the same
  time; the app alternates capture loops every frame.

## Non-HDMI audio-only alternative (no screen sync)

TV with a line-out / headphone jack (or ARC-to-3.5mm adapter) -> `[Windows box line-in]` ->
Audio Source -> Input device -> the line-in. You get full audio reactivity without the capture
card, but no screen-sync picture tracking.

## Requirements on the TV box

- Windows 10/11 64-bit, Bluetooth 4.0+ radio (mini PCs and many HDMI sticks include one).
- For Setup B: a USB HDMI capture/stick device with an audio input (they enumerate as a normal
  Windows input device; ~$15-30 on Amazon, driverless).
- LEDs follow 30-50ms behind the TV in Setup A (inherent loopback latency); Setup B adds a few
  ms from the capture card.

## What "workable" means here

The app + strip loop is identical on a TV box: audio in -> analyzer -> effects -> BLE strip.
The only TV-specific piece is which audio device feeds the analyzer, and that is now a choice:
loopback (what the box plays) or any input device (what the TV is playing).