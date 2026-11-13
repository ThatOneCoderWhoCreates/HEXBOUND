# CLAUDE.md

Instructions for Claude (and any other AI assistant) working on HEXBOUND. Read this file, then `ARCHITECTURE.md` and the latest entry in `DEVELOPMENT_LOG.md`, before making changes.

## Project facts

- Game: HEXBOUND, a 3D online 1v1 arena fighting game. Design lives in `GDD.md`.
- Engine: Unity 6.2 (`6000.2.8f1`), Universal Render Pipeline (URP) 17.2.
- Input: Unity Input System 1.14 only (Active Input Handling = Input System Package, new).
- Primary target: Windows PC (assumption, see `GDD.md` open questions).
- Project root: `C:\Users\tanma\HEXBOUND\`.
- All project-owned content lives under `Assets/_Project/`. Store or third-party assets go in `Assets/ThirdParty/`.
- Fighter models are loaded at runtime from `Assets/_Project/Resources/Fighters/<Weapon>.glb` (Sword, Staff, Wand). Record the source and license of every third-party asset in `CREDITS.md`.
- Pressing Play in any scene starts the game from `Assets/_Project/Scenes/Core/Main.unity` (see `Editor/HexboundEditorSetup.cs`).

## Hard rules

1. Never delete, overwrite or rename existing Unity assets unless the user asks for that exact change.
2. Never touch `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or the generated `.csproj` / `.sln` files. Unity regenerates them.
3. Every asset in `Assets/` has a `.meta` file holding its GUID. Never delete or hand-edit `.meta` files. Never move or rename assets from outside Unity, because doing it without the matching `.meta` breaks references. Ask the user to move assets in the Unity Editor instead.
4. New files created from outside Unity get their `.meta` file when Unity next imports them. That is expected.
5. Packages needed for development may be added without asking (approved 2026-11-13). Record each addition in `DEVELOPMENT_LOG.md` and `ARCHITECTURE.md`. Ask before removing or upgrading a package.
6. Claude cannot run the Unity Editor. After code changes, tell the user exactly what to open, press or check in Unity, and ask them to paste the Console output if something fails.
7. Keep changes small and scoped to what was asked. Do not refactor unrelated code.
8. Do not implement features that are not approved in `GDD.md` or the current task.

## Architecture rules (summary, full detail in ARCHITECTURE.md)

- The game simulation is deterministic and lives in `Scripts/Runtime/Simulation/` (assembly `HEXBOUND.Simulation`).
- Simulation code must not reference `UnityEngine`. The assembly definition enforces this with `noEngineReferences`.
- Simulation code must not use: `float` or `double` for gameplay values (use the project fixed-point type), `UnityEngine.Random` or `System.Random` (use the seeded RNG stored in game state), `DateTime` or `Time`, `Dictionary` / `HashSet` iteration order, static mutable state, threads, or LINQ inside the tick.
- All mutable match data lives in the game state, so it can be saved and restored for rollback.
- Presentation (rendering, animation, VFX, audio, camera) only reads simulation state. It never writes to it.
- Gameplay never reads Unity input directly. Unity input is converted into a per-tick input struct in `Scripts/Runtime/Input/`.
- Unity physics (PhysX) is for cosmetic effects only, never for gameplay hits or movement.
- Movement is free 3D and camera-relative. The camera is local only: the Input layer turns camera-relative input into a quantized world-space direction before it enters the InputFrame. The simulation never reads the camera.

## Version control

- Git with Git LFS, hosted on GitHub. Commit `.meta` files together with their assets.
- Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or builds (`.gitignore` handles this).
- Git commands for the user are run in their Windows terminal (Git and Git LFS installed there), not from Claude's shell.

## Code conventions

- C# namespaces match assemblies: `HEXBOUND.Simulation`, `HEXBOUND.Netcode`, `HEXBOUND.Presentation`, `HEXBOUND.Input`, `HEXBOUND.UI`, `HEXBOUND.Core`, `HEXBOUND.App`, `HEXBOUND.Editor`, `HEXBOUND.Tests`.
- PascalCase for types, methods, properties and constants. `_camelCase` for private fields. `camelCase` for locals and parameters.
- One public type per file, file name matches the type name.
- Use `[SerializeField] private` instead of public fields on MonoBehaviours.
- No `GameObject.Find`, `FindObjectOfType` or string-based lookups in runtime code.
- No allocations in per-tick code paths (simulation tick, rollback, input sampling).
- Fighter, move and projectile data live in C# content files in `Simulation/Characters/Content/`, using integer thousandths (`M(1500)` = 1.5). Rules live in `Simulation/Match/MatchRules.cs`. After changing gameplay numbers, run the EditMode tests.
- American English in code, comments and docs. Expand an abbreviation the first time it appears in a doc.

## Folder map

See `ARCHITECTURE.md` section "Folder structure". Put new files in the matching folder. If no folder fits, propose one before creating it.

## Compile checks without Unity

Unity's engine and package assemblies can be copied into `.unityrefs/` (ignored by Git) so every HEXBOUND assembly can be compiled with the .NET SDK using its `.asmdef` references. Simulation and netcode tests can also run outside Unity. Do this before handing code to the user, then still ask the user to confirm the Unity Console is clean.

## Workflow

1. Read the latest `DEVELOPMENT_LOG.md` entry to see the current state.
2. Confirm scope with the user if a request is ambiguous or touches architecture.
3. Make the change.
4. Give the user clear Unity steps to verify the change.
5. Add a dated entry to `DEVELOPMENT_LOG.md`: what changed, files touched, decisions made, open issues.
6. If a design or architecture decision changed, update `GDD.md` or `ARCHITECTURE.md` in the same session.

## Communicating with the user

- Commands for the user must be copy-pasteable, in a strict linear order, with no branching and no inline comments.
- When the user pastes Console or terminal output, treat it as the bug report and fix the root cause.
- Keep explanations in simple English and do not overclaim what was tested. Say what was verified and what was not.
