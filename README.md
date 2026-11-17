# Unbounded

A single-player arcade circuit racer built in Unity 6 and C#. The car's handling
is the product: grip and drift are explicit, tunable parameters, and the game is
one track, one car, and five AI opponents over three laps.

## Status

Milestone 1 in progress. The handling model, its Rigidbody adapter, gamepad and
keyboard input, a generated closed circuit with Kerbs and Barriers, validated
Laps with ordered Checkpoints and reset, a HUD with the lap counter and
wrong-way warning, a chase camera, the Race loop — Countdown, race timer, pause,
three-Lap finish, live position and final standings, and restart without a
scene reload — a field of five AI opponents racing the derived Racing line, and
Nitro with a draining, recharging HUD meter and the same lockout for player and
AI are in place.

## Requirements

- Unity **6000.6.9f1** (the only version this project targets)
- URP 17.6, Input System 1.20, Cinemachine 6.6, Test Framework 1.8 (resolved by
  the package manager)

Open the repo root in Unity Hub; `Assets/_Project/Scenes/Main.unity` is the
entry scene and the only one in build settings.

## Layout

```
Assets/_Project/
├── Art/          imported kits, materials
├── Audio/        engine, tyre, impact, menu loops
├── Editor/       project tooling (not shipped)
├── Prefabs/
├── Scenes/       Main.unity
├── Scripts/
│   ├── Core/     shared contracts and pure data types
│   ├── Vehicle/  arcade handling model and its Rigidbody adapter
│   ├── Track/    centreline spline, generated road, checkpoints
│   ├── AI/       racing-line follower
│   ├── Race/     countdown, laps, standings, restart
│   ├── UI/       HUD and finish screen
│   └── App/      composition root
└── Tests/        EditMode (handling, track path) and PlayMode (drives the circuit)
```

Assembly references point inward: `UI` and `App` may reference any runtime
assembly, while `Vehicle`, `Track`, `AI`, and `Race` reference only `Core`.
Illegal dependencies fail at compile time.

## Design decisions

- [ADR-0001](docs/adr/0001-custom-arcade-car-controller.md) — custom Rigidbody
  controller instead of `WheelCollider`
- [ADR-0002](docs/adr/0002-procedural-spline-track.md) — track generated from a
  centreline spline
- [GLOSSARY.md](GLOSSARY.md) — the project's domain vocabulary

## Definition of done

A build for macOS and Windows that holds 60fps, survives a ten-minute session
without errors, ships with a README explaining the handling model, and comes
with a 60-second gameplay clip.

## Controls

Handbrake arrives with a later milestone ticket.

| Action | Gamepad | Keyboard |
| --- | --- | --- |
| Steer | Left stick | A / D |
| Throttle | Right trigger | W |
| Brake / reverse | Left trigger | S |
| Handbrake | B / Circle | Space |
| Nitro | A / Cross | Left Shift |
| Reset to track | Y / Triangle | R |
| Pause / resume | Start | Escape |
| Quit from pause | Select | Q |
| Restart after the finish | Y / Triangle | R |