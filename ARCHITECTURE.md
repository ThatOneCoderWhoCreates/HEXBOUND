# HEXBOUND: Technical Architecture

Status: v0.3 (2026-11-13), updated after the M1 prototype build. Decisions marked **Approved** are final unless revisited. Anything marked **Proposed** is not final.

## 1. Goals

1. Fair online 1v1 play with low perceived input lag.
2. A game simulation that can be saved, restored and re-run exactly (needed for rollback, replays and tests).
3. Clear separation between game logic and Unity rendering, so each can change without breaking the other.
4. A structure that scales to more characters, arenas and modes.

## 2. Key decision: networking model (Approved 2026-11-13: Option A, custom rollback netcode)

Fighting games are very sensitive to input delay. The three realistic options:

| Option | How it works | Pros | Cons |
| --- | --- | --- | --- |
| **A. Custom rollback netcode (Recommended)** | Only inputs are sent. Both players run the same deterministic simulation. When a remote input arrives late, the game rolls back to that frame and re-simulates. Same approach as GGPO (Good Game Peace Out), the standard in modern fighting games. | Best feel for fighting games. Low bandwidth. No dedicated server cost. Replays come almost for free. | Most engineering work. Requires a fully deterministic simulation (fixed-point math, custom collision, no Unity physics in gameplay). |
| **B. Netcode for GameObjects (NGO), host-authoritative** | Unity's official high-level networking. One player hosts, state is synced to the other. | Fastest to get running. Well documented. | Host advantage and visible delay for the non-host player. Rollback is not built in. A weak fit for a precise fighting game. |
| **C. Third-party framework (for example Photon Quantum or Photon Fusion)** | Commercial frameworks with prediction or deterministic rollback built in. | Rollback without writing it yourself (Quantum). | Licensing and cost, vendor lock-in, Quantum uses its own entity system and workflow. |

Chosen: **Option A**, with Unity Transport for packets and the Unity Multiplayer Services package for Relay (connecting two players without opening ports) and lobbies.

## 3. Layers

```
Unity Input System
        |
        v
[Input]         Reads devices and the local camera, produces one InputFrame per tick
                (bitmask of buttons + world-space move direction)
        |
        v
[Netcode]       Exchanges InputFrames with the remote peer, predicts missing inputs,
                rolls back and re-simulates on misprediction
        |
        v
[Simulation]    Pure C#, deterministic. GameState + Tick(GameState, InputFrame p1, InputFrame p2)
        |
        v  (read only)
[Presentation]  Character views, animation sampling, camera, VFX, audio
[UI]            Menus and HUD
        ^
        |
[App]           Composition root: bootstrap, scene flow, wiring everything together
[Core]          Shared utilities: logging, service registry, events
```

Dependency rules:

- `Simulation` depends on nothing (no `UnityEngine`).
- `Input`, `Netcode`, `Presentation` and `UI` may depend on `Simulation` and `Core`.
- `App` may depend on everything. Nothing depends on `App`.
- No circular references. Assembly definitions (`.asmdef`) enforce this.

## 4. Simulation (deterministic core)

- **Tick rate:** 60 ticks per second, fixed. Driven by a time accumulator in the `App` layer, not by Unity's `FixedUpdate`.
- **Math:** a project fixed-point number type (in `Simulation/Math/`) for all gameplay values: positions, velocities, damage scaling, angles. Trigonometry uses lookup tables. Floats are not bit-identical across machines and compilers, so they are not allowed in gameplay logic.
- **Game state:** one `GameState` object holding everything that can change during a match: fighter positions, velocities, current move, frame counter inside the move, health, stun timers, round state, timer, RNG (random number generator) seed. It is made of plain value data so it can be copied quickly.
- **Snapshots:** the netcode stores the last N `GameState` copies in a ring buffer (N = max rollback window, proposed 8 frames).
- **Checksums:** a deterministic hash of `GameState`, compared between peers every few frames to detect desyncs.
- **Collision:** custom and simple. Pushboxes (keep fighters apart), hurtboxes (where a fighter can be hit) and hitboxes (where an attack hits) as capsules and spheres. The hexagonal arena boundary is six half-planes. No PhysX.
- **Movement (Approved: free 3D movement):** fighters move on the arena floor plane (X and Z) in any direction and rotate to face their movement direction. Attacks can snap facing toward the opponent within a set angle and range (soft targeting). Vertical movement (jumps, launches) uses the Y axis with simple custom gravity.
- **Input frame:** the simulation never sees the camera. The Input layer converts stick or WASD input into a world-space direction using the local camera's yaw, then quantizes it into the InputFrame (proposed: 8-bit angle, 256 directions, plus a 2-bit magnitude for walk or run, plus button bits). Both peers receive identical integer data, so movement stays deterministic.
- **Character logic:** a state machine per fighter (Idle, Walk, Dash, Attack, Block, Hitstun, Blockstun, Knockdown, and so on) driven by frame data.
- **Static data (as built):** fighters, moves and projectiles are defined in C# content files in `Simulation/Characters/Content/` (`SwordFighter.cs`, `StaffFighter.cs`, `WandFighter.cs`, `CommonMoves.cs`), built once in a fixed order by `GameContent`. Values use integer thousandths (`M(1500)` is 1.5), so no floats are involved. `GameContent.ContentHash` covers all numbers; online peers refuse to play if their hashes differ. Moving this data to ScriptableObjects is possible later, but code keeps it deterministic and easy to review.
- **Events:** the simulation emits events (hit landed, block, round end) tagged with the tick they happened on. Presentation uses these to trigger VFX and audio.

### Where to change numbers

| What | File |
| --- | --- |
| Damage, frame data, hitboxes, costs, cooldowns per fighter | `Simulation/Characters/Content/<Fighter>.cs` |
| Dodge, grab, recharge (shared) | `Simulation/Characters/Content/CommonMoves.cs` |
| Magic max, round time, parry window, chip damage, combo scaling, flight | `Simulation/Match/MatchRules.cs` (`GameConfig`) |
| Arena size | `Simulation/Collision/Arena.cs` (must match the visuals in `Presentation/ArenaView.cs`, which reads it) |

Units: time in ticks (60 per second), distance in units (about 1 meter), speed in units per tick.

### Simulation files

`GameSimulation.Tick` runs, in order: both fighters' state machines and physics (`FighterLogic`), push-apart (`FighterLogic.Separate`), projectiles (`ProjectileLogic`), hit detection and resolution with trades (`CombatResolver`), then round flow (`MatchLogic`).

## 5. Netcode (Option A)

- **Topology:** peer-to-peer 1v1 through Unity Relay. Both peers run the full simulation.
- **Transport:** Unity Transport (`com.unity.transport`), unreliable channel for input packets.
- **Input packets:** each packet carries the last several unacknowledged InputFrames (redundancy covers packet loss) plus an acknowledgement of the remote frames received.
- **Input delay:** configurable local input delay (proposed default 1 to 2 frames) to reduce how often rollbacks happen.
- **Prediction:** a missing remote input is predicted as "same as last confirmed input".
- **Rollback:** when a confirmed remote input differs from the prediction, restore the snapshot for that frame and re-simulate to the current frame within one render frame.
- **Time sync:** if one peer runs ahead, it slows down slightly so both stay within the rollback window.
- **Sessions:** Multiplayer Services (`com.unity.services.multiplayer`) for anonymous sign-in, create or join by room code, and later matchmaking.
- **Desync handling:** on checksum mismatch, log both states for debugging and end the match cleanly.
- **Trust:** peer-to-peer means a modified client can cheat. Acceptable for non-ranked play. Ranked play is out of scope.

### As built

- `Netcode/Rollback/RollbackSession.cs`: prediction, 128-tick snapshot ring, maximum prediction 8 ticks, default input delay 2 ticks (`GameSettings.InputDelay`), checksum every 30 confirmed ticks, frame-advantage time sync.
- `Netcode/Sessions/NetMatch.cs`: handshake (protocol version and content hash), character select, start (host sends seed and picks), pings, timeouts, return to select, leave. The host is always player 0.
- `Netcode/Protocol/Packets.cs`: packet types and little-endian reader and writer.
- `Netcode/Transport/UnityTransportLink.cs`: Unity Transport driver, reliable pipeline for menu messages, unreliable for inputs. Direct IP on port 7777 or Relay (DTLS).
- `Netcode/Sessions/OnlineServices.cs`: anonymous sign-in and Relay allocations; the Relay join code is the room code.
- `Netcode/Transport/SimulatedNetwork.cs`: in-memory network with latency, jitter and loss, used by tests.

## 6. Presentation

- **Views:** each fighter has a view object that copies position and rotation from `GameState` every render frame (fixed-point converted to float only here).
- **Animation (as built):** procedural. `FighterAnimator` builds a pose every frame from the fighter's state, move and move frame (limb directions, spine bend, whole-body tilt), blends toward it, and applies it through `PoseRig`, which aims each limb at a direction. Because poses come from the state, they are always right after a rollback. Bones are found by name, so imported rigs (3ds Max Biped, Character Creator, Mixamo) work without setup. Replacing this with real animation clips later should keep the rule that the pose is chosen from simulation state.
- **Models:** `FighterRigBuilder` loads `Resources/Fighters/<Weapon>` (Sword, Staff, Wand). It turns the model to face +Z (from the arm positions), scales it to the fighter's height, and puts its feet on the floor. If no model exists, or bones are missing, it builds a placeholder body from primitives. `.glb` files import through the glTFast package.
- **VFX and audio:** triggered from simulation events. Each event has a unique key (tick + type + fighter), so a rollback that replays the same event does not play it twice, and an event that is rolled back can be cancelled.
- **Camera (as built):** a small custom orbit camera (`Presentation/Camera/ThirdPersonCamera.cs`) behind the local fighter, rotated with the mouse or right stick (Genshin Impact style), with lock-on framing and screen shake. Cinemachine is installed but not used yet. Camera state is local only and is never sent over the network or rolled back.
- **Rendering:** URP with the existing `PC_RPAsset` for Windows. The existing `Mobile_RPAsset` is left in place but unused.

## 7. Scenes and flow (as built)

- One scene: `Scenes/Core/Main.unity`, containing a single GameObject with `HexboundGame` (`Runtime/App`). Everything else (camera, arena, lights, post-processing, UI, audio) is created in code.
- `Editor/HexboundEditorSetup.cs` creates that scene the first time the project loads, puts it first in Build Settings, and sets it as the Play Mode start scene, so pressing Play in any scene starts the game. Menu: HEXBOUND > Open Main Scene, Recreate Main Scene, Play From Main Scene (toggle).
- `HexboundGame` runs the screens (menu, character select, online lobby, match) and the fixed 60 Hz loop: offline matches tick `GameSimulation` directly with the local player and `TrainingBot`; online matches go through `NetMatch`.
- The template `Assets/Scenes/SampleScene.unity` is untouched.
- UI is uGUI built in code (`UI/UIFactory.cs`), using Unity's built-in font, so no UI assets or TextMesh Pro setup are needed.
- Sounds are synthesized in code (`Presentation/Audio/SfxPlayer.cs`) as placeholders.

## 8. Assemblies (created 2026-11-13)

| Assembly | Folder | References |
| --- | --- | --- |
| `HEXBOUND.Simulation` | `Scripts/Runtime/Simulation` | none (`noEngineReferences: true`) |
| `HEXBOUND.Core` | `Scripts/Runtime/Core` | none |
| `HEXBOUND.Input` | `Scripts/Runtime/Input` | Simulation, Core, `Unity.InputSystem` |
| `HEXBOUND.Netcode` | `Scripts/Runtime/Netcode` | Simulation, Core, `Unity.Networking.Transport`, `Unity.Collections`, `Unity.Burst`, `Unity.Mathematics`, `Unity.Services.Core`, `Unity.Services.Authentication`, `Unity.Services.Multiplayer` |
| `HEXBOUND.Presentation` | `Scripts/Runtime/Presentation` | Simulation, Core, `Unity.RenderPipelines.Core.Runtime`, `Unity.RenderPipelines.Universal.Runtime` |
| `HEXBOUND.UI` | `Scripts/Runtime/UI` | Simulation, Core, `UnityEngine.UI`, `Unity.InputSystem` |
| `HEXBOUND.App` | `Scripts/Runtime/App` | all of the above, `Unity.InputSystem`, `UnityEngine.UI`, URP runtime, `Unity.Networking.Transport`, `Unity.Collections` |
| `HEXBOUND.Editor` | `Scripts/Editor` | all runtime assemblies, editor only |
| `HEXBOUND.Tests.EditMode` | `Scripts/Tests/EditMode` | Simulation, Core, Netcode, test framework (editor only) |
| `HEXBOUND.Tests.PlayMode` | `Scripts/Tests/PlayMode` | all runtime assemblies, test framework |

Test assemblies only compile when the Test Framework is present (`UNITY_INCLUDE_TESTS`), so they are never included in player builds.

## 9. Folder structure

```
HEXBOUND/
├── CLAUDE.md, GDD.md, ARCHITECTURE.md, DEVELOPMENT_LOG.md
├── .gitignore, .gitattributes
├── Assets/
│   ├── _Project/                    All HEXBOUND-owned content
│   │   ├── Art/
│   │   │   ├── Animations/Characters/
│   │   │   ├── Characters/          Models, rigs
│   │   │   ├── Environment/Arenas/
│   │   │   ├── Materials/
│   │   │   ├── Shaders/             Shader Graph and HLSL
│   │   │   ├── Textures/
│   │   │   ├── UI/                  Sprites, icons, fonts
│   │   │   └── VFX/                 Particle and VFX source assets
│   │   ├── Audio/
│   │   │   ├── Mixers/
│   │   │   ├── Music/
│   │   │   └── SFX/
│   │   ├── Data/                    ScriptableObject instances
│   │   │   ├── Arenas/
│   │   │   ├── Characters/
│   │   │   ├── Config/              Match rules, netcode settings, tick rate
│   │   │   └── Moves/               Frame data, hitboxes
│   │   ├── Prefabs/
│   │   │   ├── Arenas/
│   │   │   ├── Characters/          Fighter view prefabs
│   │   │   ├── Systems/             Bootstrap, managers
│   │   │   ├── UI/
│   │   │   └── VFX/
│   │   ├── Scenes/
│   │   │   ├── Arenas/
│   │   │   ├── Core/                Bootstrap, MainMenu
│   │   │   └── Dev/                 Sandbox and test scenes
│   │   ├── Scripts/
│   │   │   ├── Runtime/
│   │   │   │   ├── App/             Composition root, scene flow, tick loop
│   │   │   │   ├── Core/            Logging, services, events, utilities
│   │   │   │   ├── Input/           Unity Input System to InputFrame
│   │   │   │   ├── Netcode/
│   │   │   │   │   ├── Rollback/    Snapshots, prediction, re-simulation, time sync
│   │   │   │   │   ├── Sessions/    Sign-in, room codes, Relay, matchmaking
│   │   │   │   │   └── Transport/   Packet format, send and receive
│   │   │   │   ├── Presentation/
│   │   │   │   │   ├── Audio/
│   │   │   │   │   ├── Camera/
│   │   │   │   │   ├── Characters/  Fighter views, animation sampling
│   │   │   │   │   └── VFX/
│   │   │   │   ├── Simulation/      Deterministic, no UnityEngine
│   │   │   │   │   ├── Characters/  Fighter state machine
│   │   │   │   │   ├── Collision/   Pushboxes, hurtboxes, hitboxes, arena bounds
│   │   │   │   │   ├── Combat/      Moves, frame data, damage, stun
│   │   │   │   │   ├── Input/       InputFrame, input buffer, motion inputs
│   │   │   │   │   ├── Match/       Rounds, timer, win rules
│   │   │   │   │   ├── Math/        Fixed-point type, vectors, trig tables
│   │   │   │   │   └── State/       GameState, snapshots, checksum
│   │   │   │   └── UI/
│   │   │   │       ├── HUD/
│   │   │   │       └── Menus/
│   │   │   ├── Editor/
│   │   │   │   ├── Inspectors/
│   │   │   │   └── Tools/           Frame data editor, hitbox visualizer
│   │   │   └── Tests/
│   │   │       ├── EditMode/        Determinism and rules tests
│   │   │       └── PlayMode/
│   │   └── Settings/
│   │       ├── Input/               Input action assets
│   │       └── Rendering/           HEXBOUND-specific URP assets and volumes
│   ├── ThirdParty/                  Asset Store and external assets, kept unmodified
│   ├── Scenes/                      (Unity template, untouched)
│   ├── Settings/                    (Unity template URP settings, in use, untouched)
│   ├── TutorialInfo/                (Unity template readme, safe to remove later)
│   └── InputSystem_Actions.inputactions  (Unity template, untouched)
├── Packages/
└── ProjectSettings/
```

Each empty folder contains a `.gitkeep` file so it survives version control. Unity ignores files whose names start with a dot.

## 10. Testing strategy

As built: `Tests/EditMode/SimulationTests.cs` (fixed-point math, input packing, determinism for all 9 matchups, snapshot replay, rollback over a simulated 100 ms / 8% loss network). Run in Unity: Window > General > Test Runner > EditMode > Run All.

Compile checks outside Unity: Unity's engine and package assemblies can be copied to `.unityrefs/` (ignored by Git) and every HEXBOUND assembly compiled against them with the .NET SDK, using each `.asmdef`'s references. This catches most errors before opening Unity. It does not replace testing in the Editor.

- **EditMode determinism tests:** run the same input sequence twice and compare checksums every tick. Save a snapshot, restore it, re-run, compare.
- **EditMode rules tests:** frame data, damage, round and timer rules.
- **Rollback tests:** simulate artificial delay and packet loss in code, check both peers end with identical checksums.
- **Manual online tests:** Multiplayer Play Mode (two player instances inside one editor) plus Unity Transport's network simulator for latency and packet loss.

## 11. Packages

### Installed and used

| Package | Version | Use |
| --- | --- | --- |
| `com.unity.render-pipelines.universal` | 17.2.0 | Rendering |
| `com.unity.inputsystem` | 1.14.2 | Input |
| `com.unity.ugui` | 2.0.0 | UI, includes TextMesh Pro in Unity 6 |
| `com.unity.timeline` | 1.8.9 | Round intros and cutscenes (optional) |
| `com.unity.test-framework` | 1.6.0 | Tests |
| `com.unity.multiplayer.center` | 1.0.0 | Editor helper for multiplayer setup |
| `com.unity.ide.visualstudio`, `com.unity.ide.rider` | | IDE support |
| `com.unity.mathematics`, `com.unity.burst`, `com.unity.collections` | (pulled in by URP) | Available. Not used inside the simulation, which uses fixed-point. |

### Added 2026-11-13 (versions released for Unity 6000.2)

| Package | Version | Why |
| --- | --- | --- |
| `com.unity.transport` (Unity Transport) | 2.6.0 | Low-level packet sending for rollback netcode |
| `com.unity.services.multiplayer` (Multiplayer Services) | 1.1.8 | Relay, Lobby, sessions, matchmaking. Pulls in Authentication. Replaces the deprecated standalone Relay and Lobby packages. |
| `com.unity.cinemachine` (Cinemachine) | 3.1.5 | Third-person camera |
| `com.unity.multiplayer.playmode` (Multiplayer Play Mode) | 1.6.3 | Run two players in one editor for online testing |
| `com.unity.cloud.gltfast` (Unity glTFast) | 6.9.1 | Imports the `.glb` fighter models |

### Later

| Package | Why |
| --- | --- |
| `com.unity.addressables` | Asset loading for characters and arenas. Add when content grows. |

### Installed but probably not needed (remove later, only with approval)

- `com.unity.visualscripting`: the project uses C#.
- `com.unity.ai.navigation`: no pathfinding needed in a 1v1 hex arena.
- `com.unity.collab-proxy` (Unity Version Control): not needed if the project uses Git.

### Required outside Unity

- A Unity Cloud project link (Edit > Project Settings > Services) for Relay and Multiplayer Services.
- A fixed-point math type. Proposed: write a small, tested one in `Simulation/Math/` to avoid licensing and portability issues.

## 12. Project settings to review

- `companyName` is still `DefaultCompany`.
- Scripting backend: Mono for development, IL2CPP for release builds (Proposed).
- No custom physics layers or tags are defined yet. They will be added only for presentation needs.
- Asset serialization is Force Text, which works well with Git.

## 13. Version control

- Git, hosted on GitHub (private repository `HEXBOUND`).
- Git LFS (Large File Storage) for binary assets: textures, models, audio, video, fonts, native plugins. The patterns are in `.gitattributes`.
- Unity YAML files (scenes, prefabs, assets, meta files) are stored as text with LF line endings.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/`, builds and the generated `.csproj` / `.sln` files.
- Branches (Proposed): `main` always opens and compiles in Unity. Work happens on short-lived feature branches, merged through pull requests.
