# HEXBOUND: Development Log

Newest entries first. Each entry: date, summary, files touched, decisions, open issues.

---

## 2026-11-15: Weapon throw (G), fists, Vex aiming and models, stairs wall, sounds

### Request (Tanmay)
G throws the equipped weapon; without a weapon everyone punches (less damage) and runs faster. Vex: models for her, the launcher, the rocket and the pistol (equip as in Docs/Concepts/vex_reference.png); a crosshair to aim rockets (a little slower than the pistol); shooting down pushes her up with recoil; good sounds. Stream quieter and more soothing. Bug: thrown toward the Sanctuary stairs, fighters fell off the map; make it an invisible wall.

### Changes
- Input: InputButtons.Throw (G, gamepad Select); InputFrame carries camera aim (AimYaw, AimPitch in 1.5 degree steps, HasAim) and packs into 64 bits; RollbackSession sends ulongs; protocol version 4.
- Simulation: FighterState WeaponsHeld/Thrown/Dropped, DropPos0/1, aim fields, RecoilJumps; ProjectileState.Carries. Throw moves and thrown-weapon projectiles built per fighter in GameContent (CharacterDef.CanThrowWeapon, ThrowDamage). FighterLogic: TryPickUp, ReleaseThrownWeapon, UpdateThrownWeapons (after combat each tick), fist moves when unarmed (CommonMoves), UnarmedSpeedScale 1.2, ApplyRecoil. ProjectileLogic.Spawn returns the slot; ProjectileDef.UseAim (crosshair with 7 degree aim assist) and RecoilLift. Vex rockets 0.7 units/tick. Arena wall arcs (Sanctuary: 20 degrees either side of north).
- Presentation: FighterVisual.HeldWeapon/WeaponTemplate; Kael's sword hidden by swapping its submesh material; Lyra gets a visible wand; Bram's staff; Vex launcher holder (shoulder or back) and pistol holder placed by the animator. Thrown weapons spin along their flight; dropped weapons lie on a pulsing glow ring; HUD prompt "G Pick up weapon". Throw pose. Rocket model for missiles. Crosshair in MatchHud for an armed gunner.
- Models: Resources/Fighters/Launcher.glb (Vex, rigged, 55k triangles, texture baked into vertex colors), VexLauncher, VexPistol, VexRocket (Tools/VexRig, Tools/ModlyPrep).
- Sounds: wx_rocket, wx_pistol, wx_throw, wx_weapondrop, wx_pickup; gun moves no longer play a sword whoosh. Stream regenerated darker and calmer, 6 dB quieter in the file and played at 0.14 (was 0.32).

### Tests
rostertest: throw and hit (55), lands on the floor, unarmed jab 18, unarmed run 7.56 m vs 6.30 m per second, pickup, Rook cannot throw, Vex launcher then pistol, rocket follows the crosshair, down-shot recoil 4.6 m without self-damage, stairs wall holds while the east bridge still rings out, aim packs and unpacks. nettest (online, Sanctuary): no desync. All suites pass.

### Open issues
- Vex's shoulder pose and the weapon placement were set from measurements of her skeleton, not seen in Unity; expect small adjustments.
- Bots never throw weapons.

---

## 2026-11-15: Cloud wall fix, part 2

### Problem
The pink haze was still there after the first fix.

### Cause and fix
CloudPuffs starts the particle system with prewarm on, so the far cloud banks were already simulated (about 120 clouds living 80 to 120 seconds) with the temporary box shape at the arena center before the ring shape was set. The banks are now cleared and restarted after the ring shape is applied.

---

## 2026-11-15: Battle sound set (replaces the silly synth tones)

### Request (Tanmay)
Sounds feel silly (the bouncy sound at the round start and so on); make them professional and battle-like.

### Changes
- New sound set, designed for the game (Tools/BattleSfx/sfx.py), 3 variants each, in Resources/Audio/SFX as wx_*.wav:
  round start/fight/end = war drum with a low gong (was a rising sine "boing"); weapon whooshes (light and heavy), dodge and jump air; parry = steel ring (was a bell); clash and guard break = metal and a heavy crack (were glass); projectile hit; explosion; cast, magic, teleport, power-up and KO rebuilt from filtered noise, low booms and soft shimmer instead of sine sweeps.
- SfxPlayer.PackMap points these sounds to the new files. Punches, blocks, landings, footsteps and UI clicks keep the Kenney recordings.

---

## 2026-11-15: Sanctuary ambience (stream and birds instead of lava)

### Request (Tanmay)
No lava sounds on the Celestial Sanctuary; a little nature sound instead, like flowing water at low volume, and birds chirping now and then, not all the time and not loud.

### Changes
- MusicPlayer.SetMap(map): in a match, the Lava Pit keeps the lava and fire layers; the Sanctuary plays only a soft stream loop (Resources/Audio/Ambience/stream.wav, volume 0.32 of a -24 dB file) and, every 14 to 40 seconds, one quiet bird call (bird_1 to bird_5, volume 0.06 to 0.11, slight pitch and left-right variation, 30% chance of a second call).
- Sounds are synthesized (Tools/AmbienceSynth/synth.py), so there are no third-party audio credits.

---

## 2026-11-15: Fix pink cloud wall inside the Sanctuary

### Problem (Tanmay, screenshots)
A thick pink haze covered the arena; the far side could not be seen.

### Cause and fix
The far cloud banks used a circle emitter whose shape scale was left at zero (set by the shared CloudPuffs helper), so every bank cloud (45 to 95 m wide) spawned at the arena center. The scale is now 1, so they spawn 160 to 230 m out, with slower drift and a little less opacity.

---

## 2026-11-15: Breakable crystal pillars

### Request (Tanmay)
If you keep attacking a crystal it should break and fall apart; about 700 health.

### Changes
- Simulation: ArenaLayout.PillarHealth (Sanctuary 700, Lava 0 = unbreakable). GameState.PillarHealth per pillar (copied for rollback, in the checksum), reset in StartRound. FighterState.PillarHitMask so each hitbox group hits a pillar once per move. CombatResolver.DamagePillar (melee), ProjectileLogic (projectile impact; blasts chip pillars in their radius). Broken pillars stop blocking movement and projectiles (Arena.SetBroken from the state every tick). New events PillarHit and PillarBreak.
- Presentation: crystals dim, shake and show a health bar when hit; on break they shatter into bouncing shards (shards past the edge fall away), leaving a dull stub; whole again next round. Sounds and bursts in EffectsPlayer.

### Tests
rostertest: Rook's jabs break a crystal in 19 hits (about 8 s), broken crystal no longer blocks, other crystals untouched, lava spikes cannot break. nettest on the Sanctuary: no desync.

### Open issues
- In Training there are no rounds, so a broken crystal stays broken until the match restarts.

---

## 2026-11-15: Arcane crystal model, clearer air over the battlefield

### Request (Tanmay)
Add the arcane crystal (last Modly piece). The map looks a little foggy; fog is fine outside the battlefield but not inside.

### Changes
- ArcaneCrystal.glb: reduced from 554k to 16k triangles; the purple shards use a glowing material (emissive strength 2.2), the rock base does not. Scaled to about 2.7 m across so it matches the pillar's solid area; the crystal light and sparkles stay.
- Sanctuary atmosphere: fog now starts at 110 m (was 60 m) and ends at 520 m, so the courtyard, terrace and gate are clear and only far scenery is hazy. Bloom softer (intensity 0.6, threshold 1.15, scatter 0.55), no extra exposure, more contrast, less lavender ambient light.

---

## 2026-11-15: Modly models for the Celestial Sanctuary

### Request (Tanmay)
Use the Modly models for the map: temple gate, cherry tree, floating island, celestial arch, waterfall cliff, shrine lantern, purple banner, stone pillar. Crystals come later.

### Changes
- Tools/ModlyPrep: reduces each export (137k to 724k triangles) to 8k to 80k triangles, keeps Modly's vertex colors, and gives the lantern's lamp faces a glowing material. Output in Resources/Arena/Sanctuary/Models.
- ArenaView.Sanctuary: Banner models hang from the swaying banner pivots; StonePillar adds six pillars at the outer bridge ends; WaterfallCliff adds four cliffs rising from the clouds; the gate portal glow moves into the Modly gate's archway. Gate, trees, lanterns, arch and far islands use the existing replacement slots.
- Credits: item 8, Celestial Sanctuary map (Pixtel Studios, made with Modly); KinemIQ is now item 9.

### Open issues
- ArcaneCrystal model pending (built-in crystals stay until then).
- Placement checked only with offline renders.

---

## 2026-11-15: Celestial Sanctuary map

### Request (Tanmay)
Add a map like the Celestial Sanctuary concept board instead of lava. Decisions: keep lava and add the Sanctuary as a second map with a map choice; round floor about the current size with four crystal pillars; Tanmay will make Modly models for the key pieces later, the rest is built in code.

### Changes
- Simulation: Arena now holds one ArenaLayout per map (edge table, pillars, pillar radius and height, spawns). MatchSetup.Map selects it; GameSimulation calls Arena.Use(map) on Initialize and every Tick. Sanctuary: radius 12, four pillars at (+-4.95, +-4.95), radius 0.9, height 4. Map is in GameState.Checksum and all layouts are in the content hash.
- Netcode: the Start message carries the map byte (protocol version 3); NetMatch.Map is the host's choice.
- Presentation: ArenaView is partial and rebuilds on SetMap. New ArenaView.Sanctuary.cs: loads Resources/Arena/Sanctuary/Sanctuary.glb, swaps in optional Modly models (FitReplace by height or size), adds the rune circle (Rune.png), edge line, crystal pillars with lights and sparkles, gate portal, waterfalls (from Anchor_Waterfall_* nodes), far floating islands, cloud sea, petals, banner sway, panoramic sky (Skybox/Panoramic, kept in builds by a new ShaderKeep material), lighting, fog and post-processing. Ring-out effect is a cloud puff on this map.
- UI and app: MAP button on the select screen (GameSettings.Map); camera background follows the map (skybox or color).
- Tools/SanctuaryBuilder: Python scripts that generate Sanctuary.glb (about 149k triangles) and its textures.

### Tests
unittest, simtest, nettest (all online runs on map 1, both sides), beasttest pass. rostertest adds: sanctuary map selected with four pillars, 12 m edge, pillar is solid, deterministic and different from lava, old setups still lava.

### Open issues
- The scene was checked only with rough offline renders; lighting, fog and cloud density need a look in Unity.
- Waiting for Modly models (gate, cherry tree, floating island, stone ring first).

---

## 2026-11-15: Dragon Mage revision (center grip, new slides, Triple Strike, combo timing)

### Request (Tanmay)
Hold the staff at its center. Replace Shockwave: C + left click a normal slide, C + right click a defensive slide, the strong slide on E. Replace Flurry with a clear triple attack. Combo order left, right, center, with more time allowed between clicks.

### Changes
- StaffFighter: Q Triple Strike (replaces Flurry), E Dragon Slide (replaces Shockwave), C + left mouse Slide, C + right mouse Guard Slide (new CharacterDef.CrouchGuardMove), combo cancel windows extended to the end of each swing.
- Combo grace for every fighter: FighterState.ComboGrace (in the checksum), MatchRules.ComboGraceTicks = 30. A light attack within 30 ticks after a combo hit ends continues the combo.
- MovePose: Strikes, GuardSlide.
- Presentation: staff grip at 50% of its length; FighterVisual.WeaponHolder turned each frame from a new Pose.StaffDir (upright idle, sideways guard, swings lead with one end, thrusts and slides point the tip forward); a swipe trail on each end of the staff.

### Tests
unittest 20/20, simtest, nettest, beasttest pass. rostertest adds: E slide 7.3 m and 80 damage, guard slide backs 3.7 m with no damage taken, Triple Strike hits 3 times, combo continues after a short pause and restarts after a long one.

### Open issues
- The staff direction in each pose is hand-tuned; check it in game and report any pose that looks wrong.

---

## 2026-11-15: Bram becomes the Dragon Mage (new model, staff, moves, golden dragon)

### Request (Tanmay)
Add the mage (mage_colored.glb) with his long staff (mageweapon_colored.glb). Left click: swing from the left, swing from the right, then push the enemy away. C + left click: slide forward with the pointy end toward the enemy. Right click: staff held sideways with both hands to defend. Ultimate: a translucent golden dragon that flies straight out and attacks (reference video provided).

### Changes
- Models: mage reduced from 280,000 to 56,000 triangles, white emission from the export removed, 17-bone skeleton generated (robe and cape follow the hips). Saved as Resources/Fighters/Staff.glb. The staff is Resources/Fighters/MageWeapon.glb, held in the right hand near the butt, tip pointing along the forearm.
- Bram (StaffFighter.cs), now "The Dragon Mage": Left Swing, Right Swing, Repelling Thrust (big push). Right click is a guard (CharacterDef.HeavyIsGuard; blocks and parries like Ctrl) with the staff held sideways. C + left click: Dragon Slide (low slide, about 4.9 m, beats a standing block). Q Flurry and E Shockwave kept. R Golden Dragon (300 magic): flies straight from the staff for about 26 m, 150 damage plus a golden blast, swallows other projectiles (ProjectileDef.Unstoppable).
- Golden dragon visual: translucent glowing gold head (horns, whiskers, bright eyes), a 26-segment body with back fins that swims along the head's path, gold sparks, light, a gold flash and roar on release and a gold blast on impact.
- Poses: staff held up and forward when idle or running, sideways staff guard, low lunge for the slide. Swipe trail along the staff.
- Note: X stays "recharge magic" for every fighter; the dragon is on R (the ultimate key).

### Verified
All assemblies compile; all automated tests pass, including new ones: right click guard blocks, slide distance and hit, the dragon flies straight, swallows a bolt and hits for 150.

### Not verified yet
How the mage model, staff placement, guard pose and the dragon look in Unity.

---

## 2026-11-15: New beast model (Gorath)

### Request (Tanmay)
Use gorilla_colored.glb (made with Modly) as the beast. Chest beat at the start, jump and pound the ground with both arms, punches, slow but strong; at low health faster, heal about 10%, and fire from the shoulder exhausts.

### Changes
- Model: 569,000 triangles reduced to 80,000 (vertex colors kept), brightened, red accents split into a glowing material, uniform white emission from the export removed. A 17-bone skeleton and skin weights were generated and saved as Resources/Fighters/Beast.glb (replaces the Chimera).
- Beast renamed Gorath (placeholder name). Light attacks are now heavy punches (left jab, right cross, hammer fist); move speed 0.088 (was 0.094); awakened speed bonus 35%.
- Animation: chest beat with thump sounds during the round intro and on victory; double-fist ground pound for Pounce, Ground Slam and the air plunge; left and right punch poses.
- Awakening (30% health): roar, 10% heal, and two fire jets from the back of the shoulder pods (orange to purple, with light) for the rest of the round.
- Credits: Chimera entries removed; Gorath credited to Pixtel Studios (made with Modly).

### Verified
All assemblies compile; all automated tests pass (EditMode 20/20, determinism soak, rollback, roster, beast learning).

### Not verified yet
How the new model, its skinning, the chest beat and the exhaust fire look in Unity.

---

## 2026-11-14: Bram and Vex, 1000 magic, flight, bot difficulty, arcade, preview, visuals

### Request (Tanmay)
More characters (Staff user, Launcher), jump heights per fighter, 1000 magic for everyone, the Wizard flies as high as she wants while magic lasts, bot difficulty, arcade tower, fighter preview, visual polish.

### Changes
- Magic: every playable fighter has 1000 (the beast still has none).
- Jump heights (meters): Lyra 2.5, Kael 3.4, Bram 3.4, Rook 4.0, Vex 4.9 (plus rocket jumps).
- Lyra's Levitate: no timer and no height cap (40 m safety limit). Drains 40 magic per second; ends when magic runs out or when R is pressed again.
- Bram (staff, StaffFighter.cs) is now in the roster. Placeholder body until a model is added as Resources/Fighters/Staff.glb.
- Vex (LauncherFighter.cs): missile launcher (explosive missiles with splash) and pistol, swapped with the mouse wheel (gamepad D-pad right). Q Cluster Missile, E Rocket Jump (shoots the floor, flies up, blasts nearby enemies; own blasts never hurt Vex), R Missile Rain (8 missiles fall around the opponent). Right click in the air also shoots down for a small boost. Placeholder body until Resources/Fighters/Launcher.glb is added.
- Engine: projectile explosions (ExplodeRadius etc.), downward and from-the-sky projectiles, weapon modes (FighterState.WeaponMode, CharacterDef.Alt* moves), InputButtons.SwitchWeapon.
- Versus Bot difficulty (Easy / Normal / Hard): Easy is the old bot, Normal and Hard use the learning brain at level 3 and 9 (learns during the match only).
- Arcade (main menu): beat the other four fighters in a random order, each sharper than the last, then the Beast. The opponents' brain keeps learning through the whole run. Lose and you can retry the stage.
- Character select redesigned for 5 fighters: card row, a turning 3D preview of the selected fighter, stats (health, magic, speed, jump) and abilities.
- Visuals: glowing swipe trails on weapons and fists during attacks, missile models with flame and smoke trails, bullet streaks, explosions (flash, fire, smoke, shockwave ring, screen shake), extra sparks and a shockwave ring on heavy hits.

### Verified
All assemblies compile. EditMode tests 20 of 20 pass. Bot soak deterministic for all 25 matchups. Rollback tests: no desyncs. Roster tests: jump order, rocket jump height and no self-damage, missile splash, weapon switch, flight drain and cancel.

### Not verified yet
Looks in Unity: placeholder bodies for Bram and Vex, the preview screen layout, trails and explosions.

---

## 2026-11-13: Crouch, X recharge, 750 magic, Fight the Beast

### Request (Tanmay)

Move recharge to X, raise magic to 750. Add crouch (C), crouch attack (C + left click) and crouch defense (C + right click). Add the monster (monke.glb): big, slightly slower, no magic, heavy punches, purple aura and faster/stronger at low health. It should learn the player's combos and get harder every time.

### Changes

- Simulation: InputButtons.Crouch; FighterAction Crouch and CrouchBlock; HitFlags Low and Overhead; MoveDef.Crouching; per-character MaxMagic (750 default); ducking rule and low guard in CombatResolver; the block arc now uses the attacker's position (blocks at very close range work). Common Low Sweep move.
- Beast: BeastFighter.cs (Chimera), awakening (BuffId.Awakened, AwakenMove), MatchMode.Beast with level-based damage. BeastMemory (saved learning) and BeastBrain (reaction delay, prediction from availability-normalized habits, parry timing, counter-picking attacks, punishing).
- Model: monke.glb had no skeleton. A skeleton (17 bones) and vertex weights were generated offline and saved as Resources/Fighters/Beast.glb.
- Presentation: crouch, sweep, roar and beast poses; awakening effects; HUD and menus (Fight the Beast button, beast info and Reset Beast in character select, credits entry).

### Verified

All assemblies compile. EditMode tests 19 of 19 pass. Bot soak deterministic. Rollback tests: no desyncs. Beast test: against a Q/E spamming player, the share of skills parried or blocked rose from 0-27% (matches 1-4) to 48-63% (matches 7-10).

### Not verified yet

How the beast model looks and animates in Unity. Balance: the beast beat the built-in bot in all test matches, including level 0.

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
