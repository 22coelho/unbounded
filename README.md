# Unbounded

A single-player arcade circuit racer built in Unity 6 and C#. 
Developed solely with the intention of making a simple car racing game, with no financial purposes.

[Watch the 60-second gameplay clip](docs/media/gameplay.mp4), captured from the
macOS build.

## Running the game

### The player builds

The definition-of-done builds are written to:

| Platform | Path |
| --- | --- |
| macOS (Apple Silicon) | `Builds/macOS/Unbounded.app` |
| Windows (64-bit) | `Builds/Windows/Unbounded.exe` |

Neither needs Unity installed. Launch the app or executable and the race starts
immediately. The built players are published on the
[v0.1.1 release](https://github.com/22coelho/unbounded/releases/tag/v0.1.1) as
`Unbounded-0.1.1-macOS-arm64.zip` and `Unbounded-0.1.1-Windows-x86_64.zip`.
`Builds/` is ignored by git, so local binaries are produced with the commands in
[Building the players](#building-the-players) rather than committed.

The macOS bundle is ad-hoc signed and not notarised, so Gatekeeper refuses the
first launch. Right-click the app and choose **Open**, or clear the quarantine
flag once with `xattr -dr com.apple.quarantine Unbounded.app`.

### In the editor

- Unity **6000.6.9f1** (the only version this project targets)
- URP 17.6, Input System 1.20, Cinemachine 6.6, Test Framework 1.8

Open the repo root in Unity Hub. `Assets/_Project/Scenes/Main.unity` is the
entry scene and the only one in build settings. Press Play.

### Building the players

From the editor, use **Unbounded > Build macOS Player** or **Unbounded > Build
Windows Player**. From the command line, the same methods build both:

```sh
unity build . --target StandaloneOSX \
  --execute-method Unbounded.Editor.BuildScript.BuildMac

unity build . --target StandaloneWindows64 \
  --execute-method Unbounded.Editor.BuildScript.BuildWindows
```

The output paths are fixed so a hand-made build and a CI build are the same
artifact: `Builds/macOS/Unbounded.app` and `Builds/Windows/Unbounded.exe`.
Windows builds need the Unity **Windows Build Support** module installed.

## Verification

`./verify.sh` is the definition of done in one command: it rebuilds
`Assets/_Project/Scenes/Main.unity` from `MainSceneBuilder`, then runs the
EditMode and PlayMode suites. Results and logs land in `Logs/verify/`, which is
ignored by git.

```sh
./verify.sh            # scene build + both suites
./verify.sh scene      # rebuild the generated scene only
./verify.sh tests      # both suites only
./verify.sh editmode   # fast suite; the pre-push hook runs this
./verify.sh capture    # record a clip from the built macOS player
```

The pre-push hook runs the EditMode suite. The scene build and PlayMode suite
stay manual: the builder rewrites the generated scene, and PlayMode takes
minutes. A scene build leaves `Main.unity` with thousands of renumbered lines;
this shows only the real changes, and `git checkout --` discards them:

```sh
git show HEAD:Assets/_Project/Scenes/Main.unity \
  | Tools/scene-diff - Assets/_Project/Scenes/Main.unity
```

Unattended runs of a built player read these flags, defined in
`Assets/_Project/Scripts/App/AutomationMode.cs`:

| Flag | Effect |
| --- | --- |
| `-autopilot` | drive the player's car with the AI racing line |
| `-fpsLog` | log frame-time averages every ten seconds and at exit |
| `-sessionMinutes N` | quit after N minutes, so a soak ends by itself |
| `-captureClip` | write a numbered JPEG sequence at a fixed 30 fps |
| `-capturePath DIR` | where the sequence goes |
| `-captureSeconds N` | clip length in seconds (default 60) |

A visual bug is reproduced, not argued about: capture it, then compare the
frames.

## Controls

| Action | Gamepad | Keyboard |
| --- | --- | --- |
| Steer | Left stick | A / D |
| Throttle | Right trigger | W |
| Brake / reverse | Left trigger | S |
| Nitro | A / Cross | Left Shift |
| Reset to track | Y / Triangle | R |
| Pause / resume | Start | Escape |
| Quit from pause | Select | Q |
| Pause menu select | D-pad up/down | Up / Down or W / S |
| Pause menu adjust | D-pad or left stick left/right | Left / Right or A / D |
| Clear best lap | X / Square | Backspace |
| Restart after the finish | Y / Triangle | R |

## The handling model

Handling is a pure C# model (`Vehicle/ArcadeHandlingModel`) stepped on the fixed
physics clock. `Vehicle/CarController` is a thin adapter: it reads the
Rigidbody's forward speed, lateral speed, and yaw rate, asks the model for a
frame of forces, and applies them at the front and rear axles. The model owns
the engine, brake, grip, yaw, and nitro curves; the adapter owns mass, contact
points, and physics integration. That split is what makes the behaviour
testable in EditMode without a scene.

**Longitudinal.** Throttle produces `EngineForce` that tapers to zero as the car
approaches `MaxSpeed` (60 m/s), so acceleration is strongest off the line and
the last few metres per second are hard-won. Brake applies `BrakeForce`; below
`BrakeToReverseSpeed` (0.5 m/s) the same input becomes reverse and is capped at
14 m/s. Rolling resistance and quadratic drag always pull the car back, and the
surface multiplies engine force, resistance, and grip.

**Lateral.** There is no simulated tyre. Lateral force is proportional to
lateral speed and `LateralGripStiffness`, clamped by `MaxLateralForce`. Grip
falls linearly from full at a standstill to `HighSpeedGrip` (0.65) at top speed,
so the car is planted in slow corners and progressively looser when fast. When
the car stops asking for grip, the lateral force falls to zero smoothly rather
than snapping.

**Yaw.** Steering is rate-limited, then a bicycle-model target yaw rate
(`speed * tan(steer) / wheelbase`) is clamped by a lateral-acceleration budget
divided by speed. The clamp is what makes the car feel speed-sensitive: at low
speed the steering angle dominates, at high speed the grip budget does.
`YawResponse` closes the error up to `MaxYawAcceleration`. Below
`GripEngageSpeed` (0.5 m/s) both lateral force and yaw are zero, so steering at
a standstill does nothing.

**Steering feel.** The stick is shaped by `SteeringCurve` before it reaches the
model. Steering sensitivity is the exponent of that curve, not a physics
parameter: it changes how much of the stick's travel is near centre, while full
lock stays full lock. The car's grip and yaw behaviour never change when the
setting moves.

**Nitro.** The tank holds 2.5 seconds of boost, recharges in 8 seconds after a
0.8-second delay, and locks out when emptied until it is back to 25%. While
boosting, `NitroForce` is added with authority tapering toward `NitroTopSpeed`
(80 m/s), so the burst raises the ceiling instead of being swallowed by drag.
The same tank and the same `CarController` drive the AI, which spends nitro
only when the next 30 metres are straight.

**Grass.** The verge multiplies engine force by 0.35, rolling resistance by 8,
and lateral grip by 0.55. Cutting a corner is slower and the car slides more,
which is the penalty the track uses instead of an invisible wall.

## The Track

Only the ordered control points are authored in the scene. `Track/TrackRoot`
derives everything else at load:

- `TrackPath` builds a closed Catmull-Rom Centreline, an arc-length table, and
  sampling by distance. Sampling wraps across the seam, and projecting a world
  position returns a distance and a lateral offset.
- `TrackMeshBuilder` sweeps the Road, Kerbs, Barriers, start/finish line, and
  edge lines along that Centreline, with colliders that follow the same geometry.
- `CheckpointRoute` places four ordered gates at fixed fractions of the lap.
  A lap counts only after every gate has been passed in order, which makes the
  start/finish line impossible to cheat by cutting.
- `TrackShape` holds the widths and heights: a 13-metre Road, raised Kerbs, and
  a Barrier beyond the Grass verge.
- `RacingLine` is derived from the Centreline and Road width. The AI follows it,
  and the same projection is used for grid placement, standings, wrong-way
  detection, and reset points.

The generated circuit is about 1.24 km per lap. The scene builder saves the
result into `Main.unity`; nothing about the road, kerbs, barriers, or checkpoints
is hand-placed.

## The race

`Race/RaceState` is pure: it owns the Countdown, the clock, the three laps, and
the finish. `App/RaceController` is the scene adapter around it. Standings order
cars by laps completed, then checkpoints passed, then distance along the Track,
and update live. Restart resets the state object and repositions every car on
the Grid without reloading the scene. The AI uses the same handling model and
collision rules as the player, and catch-up is a small pace boost that applies
only when the player is far ahead. It never teleports a car.

The chase camera is a Cinemachine rig: a rotation composer points the lens
through the corners, a damped third-person follow holds the distance, and
obstacle avoidance pulls the camera in rather than letting a Barrier block the
view. The HUD, countdown, pause, finish screen, and settings are UGUI. Engine
pitch follows speed and throttle, tyre screech follows lateral slip, and impacts
are one-shots scaled by closing speed; all audio is synthesised and diegetic.

## Layout

```
Assets/_Project/
├── Art/          imported cars, kits, sky, surfaces, and materials
├── Audio/        reserved for imported clips; current sounds are synthesised
├── Editor/       project tooling and the build script (not shipped)
├── Prefabs/
├── Scenes/       Main.unity
├── Scripts/
│   ├── Core/     shared contracts and pure data types
│   ├── Vehicle/  arcade handling model and its Rigidbody adapter
│   ├── Track/    centreline spline, generated road, checkpoints
│   ├── AI/       racing-line follower
│   ├── Race/     countdown, laps, standings, restart
│   ├── UI/       HUD and finish screen
│   └── App/      composition root, race flow, verification harness
└── Tests/        EditMode (handling, track path, race) and PlayMode (drives the circuit)
```

Assembly references point inward: `UI` and `App` may reference any runtime
assembly, while `Vehicle`, `Track`, `AI`, and `Race` reference only `Core`.
Illegal dependencies fail at compile time.
