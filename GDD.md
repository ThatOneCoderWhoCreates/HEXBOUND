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

"Hex" means both the hexagonal arena and the hexes (curses and spells) the fighters cast. Art style is TBD; the first build uses imported placeholder models and code-made effects.

## 4. Match rules

- Best of 3 rounds (first to 2 wins). If rounds are drawn, the match ends after 5 rounds at most.
- Round timer: 99 seconds.
- A round is won by reducing the opponent's health to 0 (K.O.) or knocking them off the arena (ring-out).
- On timeout, the fighter with the higher health percentage wins. Equal health is a draw.
- Health, magic and cooldowns reset each round.
- Walking, dodging and blinking can never carry a fighter off the edge. Only being hit can.

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
| Tab or middle mouse | Lock-on |
| Esc | Pause |

Gamepad: X light, Y heavy, A jump, B dodge, LB block, RB skill 1, RT skill 2, LT ultimate, D-pad down grab, D-pad up recharge, right stick click lock-on.

### Health, magic, cooldowns

- Health: 900 to 1100 depending on the fighter.
- Magic: 500 maximum, full at the start of each round. Skills cost 100 to 180, ultimates 260 to 300. No passive regeneration.
- Recharge (M): a short channel that gives +30 magic, usable every 5 seconds. You are open to attack while channeling.
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

| | Kael, the Hexblade | Bram, the Hexwarden | Lyra, the Hexcaller |
| --- | --- | --- | --- |
| Weapon | Sword | Staff | Wand |
| Style | Balanced, fast combos | Heavy, long reach, ring-outs | Fragile, ranged, mobile |
| Health | 1000 | 1100 | 900 |
| Q | Rising Slash: dash and launch (120) | Flurry: rapid multi-hit jabs (120) | Hex Bolts: three aimed bolts (100) |
| E | Hex Wave: sword projectile (150) | Shockwave: area slam, big knockback (170) | Blink: short teleport (120) |
| R | Twin Blades: dual swords for 10 s, faster 4-hit combo and stronger heavy (300) | Tempest: 3 s steerable spin that pulls in, then blasts away (280) | Levitate: fly for 8 s, shoot bolts and orbs from the air (260) |
| Model | Female warrior model (supplied by Tanmay) | Shadowkin Mage model (Sketchfab, CC-BY-4.0) | Placeholder body made of primitives |

Numbers in brackets are magic costs. Tanmay will provide more ability details later.

## 8. Arena

A floating hexagonal platform (13 units from center to corner) over a void, with a glowing rim and crystals on the corners. Falling off the edge is a ring-out.

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
2. Name and model for the Wand fighter.
3. Art direction and animation source.
4. Should magic regenerate slowly on its own as well?
