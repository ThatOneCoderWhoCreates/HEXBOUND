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
| M | Recharge magic (+50, 5 second cooldown) |
| Z | Heal (+5% of max health, 20 second cooldown) |
| Tab or middle mouse | Lock-on |
| Esc | Pause |

Gamepad: X light, Y heavy, A jump, B dodge, LB block, RB skill 1, RT skill 2, LT ultimate, D-pad down grab, D-pad up recharge, right stick click lock-on.

### Health, magic, cooldowns

- Health: 900 to 1100 depending on the fighter.
- Magic: 500 maximum, full at the start of each round. Skills cost 100 to 180, ultimates 260 to 300. No passive regeneration.
- Recharge (M): a short channel that gives +50 magic, usable every 5 seconds. You are open to attack while channeling.
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

## Update 2026-11-14: roster, flight, bots, arcade

- Roster: Kael (sword), Rook (boxer), Lyra (wizard), Bram (staff), Vex (launcher). The Beast is fought only in its own mode and in Arcade.
- Magic: 1000 for every playable fighter.
- Jump heights (meters): Lyra 2.5, Kael 3.4, Bram 3.4, Rook 4.0, Vex 4.9.
- Lyra's Levitate: no timer, no height cap (40 m safety limit); drains 40 magic per second; ends when magic runs out or R is pressed again.
- Vex: missile launcher and pistol, swapped with the mouse wheel (gamepad D-pad right). Missiles explode where they land (splash). Q Cluster Missile, E Rocket Jump (shoots the floor, flies up, blast hurts nearby enemies, never Vex), R Missile Rain. Right click in the air shoots down for a small lift.
- Versus Bot difficulty: Easy (old bot), Normal and Hard (learning brain at level 3 and 9, learns during the match only).
- Arcade: beat the other four fighters in a random order, each sharper than the last, then the Beast. Losing lets you retry the stage.

## Update 2026-11-15: Bram, the Dragon Mage (revised)

- Staff gripped at its center. Idle: staff upright. Right mouse (hold): guard, staff held sideways in front.
- Left mouse combo: left swing, right swing, center thrust that pushes the enemy away. After a combo hit ends, the next click still continues the combo for 0.5 seconds (all fighters).
- C + left mouse: Slide (45 damage, about 3.6 m, low, beats a standing block).
- C + right mouse: Guard Slide. Slips about 3.7 m backward, staff up; attacks pass through it. Holding C + right mouse afterward keeps the low guard.
- Q Triple Strike [120]: three fast staff thrusts (38, 38, 55 before combo scaling); the last knocks back. 6 s cooldown.
- E Dragon Slide [120]: long, fast slide (about 7 m, 80 damage, low, launches). 5 s cooldown.
- R Golden Dragon [300]: a translucent golden dragon flies straight ahead (150 damage plus a 60 damage burst, passes through projectiles).

## Update 2026-11-15: Celestial Sanctuary map

- Second arena, picked with the MAP button on the character select screen (saved between sessions; default Celestial Sanctuary). Online, the host's map is used. Arcade and Fight the Beast use the chosen map.
- A floating stone courtyard above a sea of clouds, based on the Celestial Sanctuary concept board. Round fighting floor, 12 m playable radius (about the size of the Lava Pit). Past the gold edge line, fighters fall into the clouds (ring-out).
- Four crystal pillars on the diagonals, 7 m from the center, block movement and projectiles up to 4 m high (cover).
- Scenery: temple gate on a terrace up the north stairs, bridges east, south and west to an outer garden ring with cherry trees, floating cliffs with pagodas and waterfalls, a broken stone ring in the sky, distant mountains and floating islands.
- Effects: rotating gold rune circle, glowing crystals, gate portal, swaying banners, falling petals, drifting clouds, panoramic sky.
- Optional Modly models (TempleGate, CherryTree, CelestialArch, Lantern, FloatingIsland, ArcaneCrystal) in Resources/Arena/Sanctuary/Models replace the built-in pieces.

- Breakable crystals (Sanctuary): each crystal pillar has 700 health. Melee hits (once per hit), projectiles and blasts chip it; a health bar shows for a few seconds after each hit and the crystal dims and shakes. At 0 it shatters, stops blocking and leaves a stub. All crystals are whole again at the start of every round. Lava Pit spikes cannot break.

## Update 2026-11-15: weapon throw, fists, Vex aiming and models

- G (gamepad: Select) throws the weapon in hand. It flies in a shallow arc, hits like a projectile (Kael 55, Lyra 35, Bram 60, Vex launcher 60 and pistol 30) and lands on the floor; if it flies off the arena it is lost for the round. Standing within 1.8 m of your own dropped weapon, G picks it up. Rook (his fists are his weapon) and the Beast cannot throw.
- Without the weapon in hand: light attacks are a jab, cross, hook combo (18, 22, 30), right click a haymaker (40), air punch and dive punch, C + left click the low sweep; Q, E and R still work. Running and air movement are 20% faster. Everything returns at the start of each round.
- Vex: throwing the launcher switches to the pistol if she still has it; with both thrown she fights bare-handed.
- Vex aiming: a crosshair shows where rockets and pistol shots go (camera aim; it snaps red onto the opponent within 7 degrees). Rockets fly at 42 m/s (pistol rounds 54 m/s). Aiming steeply down (35 degrees or more) and firing kicks Vex up (rocket jump), up to twice before landing; her own blasts never hurt her.
- Vex model (Modly): launcher on her right shoulder in launcher mode, slung diagonally on her back in pistol mode, pistol in the right hand; rockets use the rocket model.
- Celestial Sanctuary: the edge in front of the gate stairs is an invisible wall (no ring-out there); the bridge openings still ring out.

## Update 2026-11-15: Vex rework, G drop and throw, auto pickup, bridge walls

- G, all fighters who carry a weapon: tap to drop the weapon straight down at your feet; hold (0.3 s) and release to throw it. Run over your dropped weapon to pick it up (it is ready once you have stepped away from it). A weapon that falls off the arena is lost for the round.
- Without the primary weapon (Vex: the launcher), Q, E and R are locked and show an X on the HUD.
- Recharge (X) gives +50 magic for every fighter (was +30).
- Vex: new model. Max magic 600. The launcher is carried at the right hip, braced under the arm. Left click fires a rocket on every click (up to about 4 per second). Rockets rise a little, then dive onto the crosshair target (lobbed arc). Direct rocket hit 220 damage, splash 60. Right click guards, the launcher held sideways across the chest with both hands (pistol mode: off arm raised). R Missile Rain deals up to 400 if every missile lands.
- Celestial Sanctuary: invisible walls at all four walkway openings (gate stairs, east, south and west bridges). Fighters only ring out over the open edges between them.

## Update 2026-11-15: balance

- Bram's Golden Dragon (R) deals 400 on a hit (was 150).
- Vex's rockets: a direct hit deals 100 (was 220); Cluster Missile (Q) 100 direct and 50 splash (was 220 and 110). Rocket splash 60 and Missile Rain (up to 400) are unchanged.
- Vex fires one rocket every 1.75 seconds (on the ground and in the air). The first shot fires the moment you click. While reloading, the crosshair turns gray and wider.

## Update 2026-11-15: walk and run, higher jumps

- Fighters walk by default (half speed). Hold Shift (gamepad: B) to run at full speed. A quick tap of Shift (released within 0.18 s) still dodges, so a dodge now happens when the key is released.
- Jumps are higher (jump speed +8 to 9%): Kael and Bram 4.0 m, Rook 4.7 m, Lyra 3.0 m, Vex 5.8 m. The Beast jumps higher too.
- Bots and the Beast always run when they move.
- Fix: holding G now registers (the key was only read on the press), so hold-G throws work.

## Update 2026-11-15: Sky Ruins map, graphics, recorded sounds, training checklist, smarter bots

- **Sky Ruins (third map, MAP button):** a long cracked courtyard on a mountain top with real high ground. Two spawn platforms (north and south, 1.6 m high) with stairs down; four corner terraces (1.3 m) with stairs, two carrying ruined temples whose columns are solid; broken walls (1.4 to 1.6 m) and broken columns for cover; side alcoves behind low walls; a collapsed altar (0.8 m) in the middle. Fighters walk up stairs, step down low ledges, fall off higher ones, jump onto walls and platforms, and are stopped by walls and columns. Rockets, bolts and thrown weapons hit walls and platforms (cover works). Knocked off a cliff = ring-out; the stairs behind both spawns and the paths out of the side alcoves are invisible walls. Rock spires rise from the mist around it, snowy ridges stand far away, two Modly temples sit on neighboring peaks. Mountain wind ambience, rare birds.
- **Camera:** moves in front of walls and platforms that would block the view, then eases back out.
- **Graphics:** dust kicked up on landings, jumps, dodges, knockdowns, explosions and running footsteps (colored per map); hot spark streaks on hits, blocks, parries and clashes; stone chips from explosions and slams; a soft contact shadow under each fighter that stays on the floor or platform below while jumping; 4x anti-aliasing and 70 m shadows in built games.
- **Sound:** weapon swings, throws, clashes, parries, blocks, dodges (cloth), weapon drops and pickups and footsteps are now real CC0 recordings (Kenney Impact Sounds and RPG Audio, qubodup swishes, StarNinjas swords).
- **Training:** a "How to play" checklist on the left ticks off each basic control as you do it (F3 hides it).
- **Bots:** jump when stuck at a wall or ledge or when the player stands on a platform above them, run back to pick up a dropped weapon, sometimes throw their weapon (hold G), and Vex bots fire rockets.
- **Bug fixes:** a Levitating fighter's diving attack could sink through the floor (now stops on the floor); a fast fall onto a platform could slip inside it.

## Update 2026-11-15: bigger Sky Ruins, solid roofs, breakable temples and walkways, run by default

- Fighters run by default. Hold Shift (gamepad: B) to walk; a quick tap still dodges.
- Sky Ruins is twice as wide and twice as long (4 times the area): about 44 x 52 m, spawn platforms out to 34 m, side alcoves out to 27 m. Spawns are 58 m apart.
- Temple roofs are solid: you can jump onto them from the courtyard (top 4.0 m), walk under them on the terrace (underside 3.5 m) and bump your head on them.
- New raised stone walkways on both sides (deck 2.6 to 3.2 m, with stairs): walk on top or underneath.
- The two temples and the two walkways are breakable structures with 2000 health each. Weapon hits, projectiles and blasts damage them (a health bar shows while they take damage). At 0 they collapse into rubble: anyone underneath is crushed (knocked out), anyone on top falls.
- Roofs and decks shield from attacks from above: Missile Rain hits the roof instead of a fighter sheltering underneath (and damages the roof). Blasts no longer hurt through walls, roofs or decks.

