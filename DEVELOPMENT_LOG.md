# HEXBOUND: Development Log

Newest entries first. Each entry: date, summary, files touched, decisions, open issues.

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
