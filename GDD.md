# HEXBOUND: Game Design Document (GDD)

Status: v0.3 (2026-11-13). Decisions below were made with Tanmay on 2026-11-13. Numbers are first-pass values, tuned in code (see `ARCHITECTURE.md`, "Where to change numbers").

## 1. Overview

| Item | Value |
| --- | --- |
| Title | HEXBOUND |
| Genre | 3D arena fighting game with weapons and magic |
| Mode | Online 1v1 player versus player (PvP), plus offline training and versus bot |
| Engine | Unity 6.2, Universal Render Pipeline (URP) |
| Platform | Windows PC |
| Input | Keyboard and mouse first, gamepad supported |
| Session length | Best of 3 rounds, 99 seconds each |

One-line pitch: two hex-wielding fighters battle on a floating hexagonal arena, mixing weapon combos with magic abilities, over rollback netcode.

## 2. Design pillars

1. **Readable combat.** Every attack has clear startup, active and recovery frames.
2. **Fair online play.** Rollback netcode, so online feels close to offline.
3. **Weapon plus magic.** Each fighter's abilities match their weapon.

## 3. Theme

"Hex" refers to the hexes (curses and spells) the fighters cast. The arena was a hexagon in early builds and is now a floating island over a lava arena. Art style is TBD; the first build uses imported placeholder models and code-made effects.

## 4. Match rules

- Best of 3 rounds (first to 2 wins). If rounds are drawn, the match ends after 5 rounds at most.
- Round timer: 99 seconds.
- A round is won by reducing the opponent's health to 0 (K.O.) or knocking them off the arena (ring-out).
- On timeout, the fighter with the higher health percentage wins. Equal health is a draw.
- Health, magic and cooldowns reset each round.
- Walking, dodging and blinking can never carry a fighter off the island's edge. Only being hit can.

## 5. Movement (free 3D, camera-relative)

- WASD moves relative to the camera, in any direction, like Genshin Impact or Black Desert Online.
- The fighter turns to face where it moves. Attacks turn toward a nearby opponent automatically (soft targeting: within 8 units and about 75 degrees).
- Lock-on (Tab or middle mouse): the fighter always faces the opponent and strafes; the camera keeps the opponent in view.
- Jump (Space). Air attacks: light attack in the air, heavy attack in the air is a plunge or burst.
- Dodge (Shift): short dash with brief invulnerability, 0.6 second cooldown. In the air it becomes an air dash (once per jump).

## 6. Combat

### Controls (keyboard and mouse)

| Key | Action |
| --- | --- |
| Left mouse | Light attack, 3-hit combo (4 hits with Twin Blades) |
| Right mouse | Heavy attack, breaks a normal block |
| Q, E | Skills |
| R | Ultimate |
| Shift | Dodge / air dash |
| Space | Jump (rise while flying) |
| Left Ctrl | Block (hold). Parry: start blocking at most 8 frames (0.13 s) before a hit |
| F | Grab (beats block and parry, misses airborne targets) |
| M | Recharge magic (+30, 5 second cooldown) |
| Z | Heal (+5% of max health, 20 second cooldown) |
| Tab or middle mouse | Lock-on |
| Esc | Pause |

Gamepad: X light, Y heavy, A jump, B dodge, LB block, RB skill 1, RT skill 2, LT ultimate, D-pad down grab, D-pad up recharge, right stick click lock-on.

### Health, magic, cooldowns

- Health: 900 to 1100 depending on the fighter.
- Magic: 500 maximum, full at the start of each round. Skills cost 100 to 180, ultimates 260 to 300. No passive regeneration.
- Recharge (M): a short channel that gives +30 magic, usable every 5 seconds. You are open to attack while channeling.
- Heal (Z): a short channel that restores 5% of maximum health (50 of 1000), usable every 20 seconds. Does nothing at full health. You are open to attack while channeling.
- Every skill and ultimate also has its own cooldown.

### Defense

- **Block** reduces damage to 10% chip damage and covers the front of the fighter.
- **Parry** (block timed just before a hit): no damage, and the attacker is stunned for 40 frames. Projectiles are destroyed.
- **Heavy attacks** break a normal block (guard break: 50 frames of stun). They can still be parried.
- **Grabs** ignore block and parry.
- **Dodge** is invulnerable for most of its duration.

### Hits

- Light hits cause short hitstun. Launchers and heavy hits send the target airborne (juggle), then they land in a knockdown and get up with brief invulnerability.
- Damage scales down 8% per hit in a combo (minimum 40%). Gravity increases with each juggle hit, which stops infinite air combos.
- Hits freeze both fighters for a few frames (hitstop) for impact.

## 7. Fighters

| | Kael, the Hexblade | Rook, the Hexfist | Lyra, the Hexcaller |
| --- | --- | --- | --- |
| Type | Swordsman | Boxer | Wizard |
| Style | Balanced, fast combos | Fastest on foot, short reach, punch strings | Fragile, ranged, flies |
| Health | 1000 | 1000 | 900 |
| Move speed (units/s) | 6.3 | 7.3 | 6.7 |
| Light combo | 3 hits (4 with Twin Blades) | Jab, cross, hook, body blow | 2 pokes and a spark |
| Heavy | Hex Cleave | Uppercut (launches) | Hex Orb (projectile) |
| Q | Rising Slash: dash and launch (120) | Rapid Punches: fast multi-hit barrage (110) | Hex Bolts: three aimed bolts (100) |
| E | Hex Wave: sword projectile (150) | Dash Punch: lunging straight (120) | Blink: short teleport (120) |
| R | Twin Blades: dual swords for 10 s (300) | Final Round: 2 s steerable barrage, then a huge uppercut (280) | Levitate: fly for 8 s, shoot from the air (260) |
| Model | Female warrior (supplied by Tanmay) | "Arcane Inspired Character" (Sketchfab, CC-BY-4.0) | "PBR Shadowkin Mage" (Sketchfab, CC-BY-4.0) |

Numbers in brackets are magic costs. The Staff fighter (Bram) is designed and coded but out of the roster until a staff model is found. More fighters will be added later.

## 8. Arena

Fighters battle on top of a floating island (playable radius about 10 to 12.9 units, following the island's real cliff edge). The island floats in the middle of a giant lava arena: the lava stage model with its own center stage removed, scaled up 3 times as the background, with a lava pool far below, rock spikes around it, embers rising and lava light from beneath. Being knocked off the island's edge is a ring-out.

## 9. Game modes

| Mode | Status |
| --- | --- |
| Training (vs. bot: idle, block or fight), with optional infinite magic | Built |
| Versus bot | Built |
| Online private match by room code (Unity Relay) | Built |
| Online by direct IP (LAN, or two copies on one PC) | Built |
| Local versus on one PC (split-screen) | Later |
| Matchmaking, ranked | Later |

## 10. Camera

Third-person orbit camera behind the local fighter, rotated with the mouse or right stick. Lock-on swings it to keep the opponent in view. Screen shake on big hits.

## 11. UI

Main menu, character select (with opponent pick and training options), online lobby (room code or IP), HUD (health with damage trail, magic, round pips, timer, ability cooldowns, combo counter, damage numbers, lock-on marker, network stats), pause menu (controls, hitboxes, mouse sensitivity), results.

## 12. Out of scope for now

Progression, cosmetics, store, chat, spectating, replays, ranked, cross-platform, real animations and audio (placeholders for now).

## 13. Milestones

1. **M0: Foundation.** Done.
2. **M1: Playable prototype.** Built 2026-11-13: deterministic simulation, 3 fighters, rollback netcode, menus, HUD. Needs play-testing and tuning.
3. **M2: Feel and polish.** Real animations (for example from Mixamo), sound, tuning from play-tests.
4. **M3: Vertical slice.** Final art for one fighter and the arena.

## 14. Open questions

1. Ability details for each fighter (Tanmay to provide).
2. Final names for all fighters (current names are placeholders), and a staff model to bring Bram back.
3. Art direction and animation source.
4. Should magic regenerate slowly on its own as well?

## Update 2026-11-13: crouch, magic changes, Fight the Beast

- Recharge magic moved from M to X. Every fighter now has 750 magic (was 500).
- C (hold): crouch. Attacks centered above 62% of the fighter's height miss. Grabs still connect.
- C + left mouse: Low Sweep (50 damage, trips). A standing block does not stop it.
- C + right mouse (hold): low guard. Blocks low and normal attacks, is never guard-broken (heavies deal double chip), cannot parry. Loses to overheads (Overhead/Slam moves), air attacks and grabs.
- Fight the Beast (main menu): best of 3 against Chimera, a large beast (1300 health, no magic, slower, heavy punches). At 30% health, once per round, it awakens: roar shockwave, heals 10%, purple aura, then 30% faster movement, faster attacks and 25% more damage for the rest of the round.
- The beast learns the player's habits (which actions in which situations, what follows what, how eagerly each skill is used when available, how the player defends). Expected moves are noticed almost at once and parried or blocked; attacks are chosen to beat the player's usual defense. Memory and level are saved on the PC; the level rises each time the player wins (0 to 10). Higher levels react faster, parry more and deal more damage (75% at level 0 to 115% at level 10).
