# HEXBOUND: Game Design Document (GDD)

Status: Draft v0.1 (2026-11-13). Items marked **TBD** or **Proposed** need a decision from the team before they are built.

## 1. Overview

| Item | Value |
| --- | --- |
| Title | HEXBOUND |
| Genre | 3D arena fighting game |
| Mode | Online 1v1 player versus player (PvP) |
| Engine | Unity 6.2, Universal Render Pipeline (URP) |
| Platform | Windows PC (Proposed). Console and other platforms are out of scope for now. |
| Input | Gamepad first, keyboard and mouse supported |
| Session length | About 3 to 5 minutes per match (Proposed) |

One-line pitch: two fighters meet on a hexagonal arena and fight in short, readable rounds over a fair online connection.

## 2. Design pillars (Proposed)

1. **Readable combat.** Every attack has clear startup, active and recovery frames, with visual tells a player can learn and react to.
2. **Fair online play.** Online matches should feel close to offline matches. This is why the architecture uses rollback networking (see `ARCHITECTURE.md`).
3. **Short, focused matches.** Fast to start, fast to finish, easy to rematch.

## 3. Core loop

1. Player opens the game and picks a mode.
2. Player creates or joins a match (room code first, matchmaking later).
3. Both players pick a character.
4. Players fight a best-of-3 round match on a hexagonal arena.
5. Results screen, then rematch or return to menu.

## 4. Match rules (Proposed defaults, all tunable in config data)

- Best of 3 rounds.
- Round timer: 60 seconds (TBD, 60 to 99).
- Each fighter has one health bar. A round ends when one fighter reaches 0 health or the timer runs out.
- On timeout, the fighter with more remaining health (as a percentage) wins the round. A tie gives each fighter a round (TBD).
- Ring-out: a fighter knocked off the arena edge loses the round (TBD, depends on arena design).

## 5. Movement (Decided 2026-11-13: free 3D movement)

Fighters move freely in all directions on the arena floor, similar to action games and MMORPGs (massively multiplayer online role-playing games) such as Genshin Impact or Black Desert Online.

- Movement is camera-relative: pushing the stick or W moves the fighter away from the camera, A and D move left and right relative to the camera view.
- The fighter turns to face the direction it is moving.
- Attacks turn the fighter toward the opponent if the opponent is within a set angle and range (soft targeting, Proposed), so attacks do not whiff only because of facing.
- A lock-on toggle that keeps the fighter facing the opponent while moving (strafing) is Proposed, TBD after the first prototype.

Movement actions (Proposed): walk or run, dash or dodge in any direction, jump (TBD).

## 6. Combat

All combat is driven by frame data at a fixed 60 ticks per second.

- **Attacks:** light, heavy, special (Proposed three buttons), plus directional variations.
- **Defense:** hold block, with chip damage TBD. Throws beat block, attacks beat throws.
- **Frame data per move:** startup, active, recovery, damage, hitstun, blockstun, knockback, hitbox shapes per frame, cancel windows.
- **Hit reactions:** hitstun, blockstun, knockdown, wake-up.
- **Combos:** attacks can cancel into other attacks during defined cancel windows.
- **Meter or super moves:** TBD.

## 7. Characters

- Prototype target: 2 characters with distinct play styles (for example, one fast close-range fighter and one slower long-range fighter). Names, looks and move lists are TBD.
- Each character is authored as data: stats (health, walk speed, weight), a move list, hurtbox setup, animations.

## 8. Arena

- A hexagonal platform, which ties into the HEXBOUND name.
- Prototype: one flat hex arena with clear edges.
- Ideas for later (TBD): hex tiles that change during a round, hazards, multiple arena themes.

## 9. Game modes

| Mode | Priority | Notes |
| --- | --- | --- |
| Training (vs. dummy) | Milestone 1 | Needed to build and tune combat |
| Local versus (2 controllers, 1 PC) | Later | Each player needs their own camera, so this needs split-screen. For M1 testing, a developer-only mode lets a second controller drive the other fighter. |
| Online private match (room code) | Milestone 2 | Core online mode |
| Online matchmaking (quick match) | Later | |
| Ranked play | Out of scope for now | |

## 10. Camera

- Each player has their own third-person camera behind their fighter, rotated with the mouse or right stick, as in Genshin Impact.
- The camera only affects what the local player sees and the direction their movement input points. It is not part of the shared game state.
- Optional assist that turns the camera toward the opponent (Proposed, TBD).

## 11. User interface (UI) and heads-up display (HUD)

- Menus: Title, Main Menu, Character Select, Online (create or join room), Settings, Results.
- HUD: both health bars, round counter, round timer, connection quality (ping and rollback frames) during online play.

## 12. Art and audio direction

TBD. Placeholder art (simple shapes and free assets) is used until the core game is fun.

## 13. Out of scope for the first playable version

Progression systems, cosmetics, store, chat, spectating, replays (the architecture should allow replays later), ranked ladders, cross-platform play.

## 14. Milestones (Proposed)

1. **M0: Foundation.** Folder structure, docs, packages, assembly definitions, Git. (Current)
2. **M1: Offline prototype.** Deterministic simulation, one arena, two placeholder fighters, free movement, basic attacks, block, hits, rounds. Training mode.
3. **M2: Online prototype.** Rollback networking over Unity Relay, private room codes.
4. **M3: Vertical slice.** Real art for one character and the arena, full UI flow, polish.

## 15. Open questions

1. Soft targeting angle and range, and whether to add a lock-on toggle.
2. Ring-out: yes or no?
3. Round timer length and timeout rules.
4. Meter or super moves?
5. Platforms beyond Windows?
6. Character concepts and art style.
