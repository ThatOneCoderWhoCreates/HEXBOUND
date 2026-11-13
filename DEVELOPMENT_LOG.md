# HEXBOUND: Development Log

Newest entries first. Each entry: date, summary, files touched, decisions, open issues.

---

## 2026-11-13: Motion-capture animations, Z heal, Windows build

### Request (Tanmay)

Use the fighting animations from the KinemIQ free pack. Add Z to heal a small amount (5%) with a 20 second cooldown. Then export a Windows .exe to share with a friend to test online play.

### Changes

- **Animations:** 35 CC0 clips plus the mannequin copied to `Resources/Animations/KinemIQ/`. New `MocapLibrary` plays a clip on a hidden mannequin through a PlayableGraph and reads limb, spine and pelvis directions; `FighterAnimator` copies them onto each fighter (retargeting by direction, so it works on all three rigs). New `MocapBindings` picks the clip per state: jab, cross and hook for the Boxer; sword slash, sword idle and sword block for the Swordsman; sprint, jump, hit reactions, knockback, fall, get-up, forward dodge roll and victory dance for everyone. Attack clips are time-warped so the clip's impact frame lands on the first active hitbox frame. Anything without a clip (spells, flying, grabs, kicks) keeps its procedural pose. Gameplay timing is unchanged; animation is visual only.
- **Editor:** `HexboundAnimationImport` imports the KinemIQ files as Generic rigs. `HexboundWindowsBuild` adds HEXBOUND > Build Windows Game (.exe + zip).
- **Heal:** new Z button (gamepad: D-pad left). Common move "Heal": 30 tick channel, +5% max health at tick 14, 20 second cooldown, usable in the air. New HUD slot, green effect and "+50 HP" popup. Training bot heals below 40% health. Network protocol version raised to 2 (older builds cannot connect).
- **Tests:** new EditMode test `HealRestoresFivePercentWithCooldown`.

### Verified

All runtime and editor assemblies compile against Unity's libraries. EditMode tests 15 of 15 pass. Bot soak 360,000 ticks deterministic. Rollback over simulated networks: no desyncs.

### Not verified yet

How the motion-capture poses look on each model (needs the Unity Editor). The Windows build has not been run yet.

---

## 2026-11-13: New arena - floating island inside the lava stage

### Request (Tanmay)

Fight on the floating island. Put it in the center of the low poly lava fighting arena stage, with that stage's own center stage removed and the stage made much bigger as the background.

### Changes

- Models added: `Resources/Arena/Island/floating_island_2k.fbx` (+ 9 textures) and `Resources/Arena/LavaStage/Lava_Stage.fbx` (+ 6 textures). Inspected offline: the lava stage has parts `sky`, `Outer`, `base` (center stage) and `lower` (lava pool); the island has `Island`, `Roots`, `Rocks`.
- **Simulation:** the arena is no longer a hexagon. `Arena.cs` now holds the island's measured edge as 72 fixed-point radii (playable radius 9.83 to 12.88, median 11.72). The island top is flat to about +/-0.2 units across the playable area. Edge table added to the content hash.
- **Presentation:** `ArenaView` rewritten: island scaled by 0.0105 and placed so its top sits at height 0 and its edge matches the rules; lava stage with `base` hidden, scaled 3x, 35 units below; flowing glowing lava, unlit sky dome, ember particles, lava up-lights, warm fog and color grading. Fallback floor if models are missing.
- **Camera:** far clip 450 for the larger background; menu orbit moved out to show the scene.
- **Tests:** the hexagon checks were replaced with island edge checks (EditMode test `IslandEdgeIsConsistent`).

### Verified

All 9 runtime and editor assemblies compile against Unity's libraries. EditMode tests 14 of 14 pass. Bot soak (360,000 ticks, all matchups) deterministic on the new floor. Rollback over simulated networks: no desyncs.

### Not verified yet

How it looks in the Editor: the import scale and orientation of the two FBX files, whether their materials pick up their textures automatically, and lava glow strength.

---

## 2026-11-13: Music and ambience

- Added `intro.mp3` (66 s), `loading.mp3` (131 s), `flowinglava.mp3` (40 s) and `fire.mp3` (594 s) to `Resources/Audio/Music/`. Supplied by Tanmay; sources and licenses still to be recorded in `CREDITS.md`.
- New `MusicPlayer`: main menu plays intro; character select and online lobby play loading; matches play lava and fire ambience at very low volume (Tanmay asked that fire stay very quiet so it does not distract). Tracks crossfade when the screen changes.
- Measured loudness: fire.mp3 is about 24 dB quieter than flowinglava.mp3, so the match volumes are balanced (lava 0.06, fire 0.35, times the master volume).
- New editor script `HexboundAudioImport`: music files import as compressed streaming audio, so the 10-minute fire track is not decompressed into memory.
- Compiles against Unity's libraries with 0 errors. Not yet heard in the Editor.

---

## 2026-11-13: Audio - Kenney Impact Sounds

- Added 75 clips from Kenney Impact Sounds (CC0) to `Resources/Audio/SFX/`, with its license file. Credited in `CREDITS.md`.
- `SfxPlayer` now loads recorded clips by file-name prefix and plays a random variant each time. Sounds the pack does not cover (swings, casts, magic, teleport, buff, announcer, explosion, KO boom) still use the synthesized placeholders.
- Mapping: hits use punch medium and heavy; block uses metal; parry uses bell; guard break uses glass heavy; jump and landing use soft impacts; throws and knockdown landings use plank; projectile hits and clashes use glass; UI uses generic and wood light; "not enough magic" uses tin.
- New: footsteps while running, timed to the run animation (`FighterView.StepThisFrame`).
- Compiles against Unity's libraries with 0 errors. Not yet heard in the Editor.

---

## 2026-11-13: Roster change - Boxer added, Staff fighter benched

### Changes

- Roster is now Swordsman (Kael, female warrior model), Boxer (Rook, new), Wizard (Lyra, mage model). Requested by Tanmay.
- New `BoxerFighter.cs`: move speed 0.122 (Swordsman 0.105), 4-hit punch combo, uppercut heavy, Q Rapid Punches, E Dash Punch, R Final Round. E and R were proposed by Claude and can be changed.
- `StaffFighter.cs` kept but not registered in `GameContent` until a staff model is found.
- `CharacterDef` gained `ModelName` (model file in `Resources/Fighters`) and `Stance` (boxing guard). New pose `JabLeft`. Flurry moves now end in an uppercut or straight pose for their finisher.
- Models: `Boxer.glb` added ("Arcane Inspired Character" by schs05, CC-BY-4.0, Mixamo rig). `Staff.glb` renamed to `Wizard.glb` (with its `.meta`). Credits updated in `CREDITS.md` and the main menu.

### Verified

All runtime assemblies compile against Unity's libraries (0 errors). EditMode tests 13 of 13 pass. Determinism soak passes for all 9 matchups including the Boxer. Not yet run in the Unity Editor.

---

## 2026-11-13: M1 playable prototype (full first build)

### Design decisions (Q&A with Tanmay)

Placeholder visuals from primitives, plus models Tanmay supplied. "Hex" means both the arena and magic. Weapon fighters with weapon-themed abilities. Keyboard and mouse first. Three fighters (Sword, Staff, Wand). Two skills and one ultimate each. Dodge, block and parry. Best of 3, 99 seconds, ring-out on. Light combo plus heavy, with grab. Jump with air attacks. Soft targeting plus lock-on. Magic: 500 max, skills 100 to 200, ultimates 250 to 300, recharge key M (+30, 5 s cooldown), unlimited stamina. Full details in `GDD.md`.

### Built

- **Simulation** (25 files): fixed-point math with table-based trig, input frame, game state with checksum, fighter state machine, moves with frame data, projectiles, hit resolution with trades, block, parry, guard break, grab, combo scaling, juggle gravity, magic and cooldowns, buffs (Twin Blades, Levitate flight), hexagonal arena with edge protection and ring-outs, round and match flow, training mode, three fighter kits, and a training bot.
- **Netcode** (7 files): rollback session, match handshake and lobby, packet protocol, Unity Transport link (Relay and direct IP), online services (sign-in and Relay room codes), simulated network for tests.
- **Input**: keyboard, mouse and gamepad to a camera-relative input frame, with presses between ticks kept.
- **Presentation** (10 files): arena, lighting, fog and post-processing; fighter models via glTFast with bone detection and procedural animation; placeholder primitive fighter; effects (hits, blocks, parries, rings, sparks, projectiles with trails and lights); synthesized sound effects; third-person camera with lock-on and shake; optional hitbox display.
- **UI** (6 files): main menu, character select, online lobby, pause, controls, results, and HUD.
- **App**: `HexboundGame` composition root and screen flow. **Editor**: auto-creates `Scenes/Core/Main.unity` and plays from it.
- **Models**: `Resources/Fighters/Sword.glb` (female warrior, from Tanmay), `Resources/Fighters/Staff.glb` (Shadowkin Mage, CC-BY-4.0, credited in `CREDITS.md`). The Wand fighter uses the placeholder body.
- **Package added**: `com.unity.cloud.gltfast` 6.9.1.
- **Assembly references changed**: Presentation uses URP runtime instead of Cinemachine; UI adds Input System; App adds Transport and Collections; EditMode tests add Netcode.

### Verified (outside Unity)

- All 9 assemblies compile against Unity 6000.2.8f1's engine and package assemblies with zero errors and zero warnings. The EditMode test assembly was compiled in a standard .NET project with NUnit, because of a local reference limitation.
- EditMode tests: 13 of 13 pass when run with .NET 8.
- Determinism: 360,000 bot-versus-bot ticks across all 9 matchups give identical checksums on repeat runs.
- Rollback: two peers over simulated networks (0 to 120 ms latency, up to 10% loss) finish full matches with no desync and identical results.

### Not verified yet

- Running in the Unity Editor: visuals, model orientation and scale, procedural animation quality, UI layout, feel, and Relay online play. Needs Tanmay's play-test.
- Relay needs the project linked to Unity Cloud (Edit > Project Settings > Services).

---

## 2026-11-13: M0 Foundation, decisions, packages, assemblies, Git

### Decisions (approved by Tanmay)

1. Networking: custom rollback netcode (Option A) over Unity Transport, with Multiplayer Services for Relay and lobbies.
2. Movement: free 3D movement in all directions, camera-relative, Genshin Impact or Black Desert Online style. Each player has their own orbit camera.
3. Claude may install any package needed for development.
4. Use Git with GitHub.

### Changes

- `Packages/manifest.json`: added `com.unity.transport` 2.6.0, `com.unity.services.multiplayer` 1.1.8, `com.unity.cinemachine` 3.1.5, `com.unity.multiplayer.playmode` 1.6.3. No packages removed.
- Created 10 assembly definitions (see `ARCHITECTURE.md` section 8). `HEXBOUND.Simulation` has no engine references.
- Added `.gitignore` and `.gitattributes` (Git LFS for binary assets).
- Updated `GDD.md` (movement, camera, modes, milestones) and `ARCHITECTURE.md` (decisions, input frame, camera, assemblies, packages, version control).
- Added `README.md`.
- Git initialized with Git LFS. First commit pushed to the private GitHub repository `ThatOneCoderWhoCreates/HEXBOUND` on branch `main` (205 tracked files, no `Library/`, `Temp/`, `Logs/`, `UserSettings/` or generated project files).

### Open issues

- Package resolution and assembly compile must be confirmed in the Unity Editor (Console should show no errors).
- Local versus on one PC needs split-screen, moved to "Later" in `GDD.md`.
- Scenes (Bootstrap, MainMenu, Sandbox, first arena) not created yet. They are easier and safer to create inside the Unity Editor.

---

## 2026-11-13: M0 Foundation, project setup

### Summary

Inspected the fresh Unity project, created the folder structure and the project docs. No gameplay code was written.

### Project inspection findings

- Unity 6.2 (`6000.2.8f1`), created from the URP 3D template.
- Active Input Handling: Input System Package (new) only.
- Assets present: `Scenes/SampleScene.unity`, `Settings/` (PC and Mobile URP assets, volume profiles), `TutorialInfo/` (template readme), `InputSystem_Actions.inputactions`.
- No custom scripts, no assembly definitions, no custom tags or layers.
- No version control in the project folder.
- Networking, camera and services packages are not installed yet (see `ARCHITECTURE.md` section 11).

### Files and folders created

- `Assets/_Project/` with the full subfolder tree from `ARCHITECTURE.md` section 9 (each leaf folder has a `.gitkeep`).
- `Assets/ThirdParty/`
- `CLAUDE.md`, `GDD.md`, `ARCHITECTURE.md`, `DEVELOPMENT_LOG.md` in the project root.

### Files not touched

All existing assets, `.meta` files, `Packages/`, `ProjectSettings/`.

### Decisions pending approval

1. Networking model: custom rollback netcode (recommended) vs. Netcode for GameObjects vs. third-party.
2. Movement model: free 3D movement vs. 2D plane with sidestep.
3. Package additions: Unity Transport, Multiplayer Services, Cinemachine, Multiplayer Play Mode.
4. Set up Git and Git LFS.

### Next steps (after approval)

1. Add the approved packages.
2. Create the assembly definitions from `ARCHITECTURE.md` section 8.
3. Create the Bootstrap, MainMenu, Sandbox and first arena scenes.
4. Start M1: fixed-point math type and its tests.
