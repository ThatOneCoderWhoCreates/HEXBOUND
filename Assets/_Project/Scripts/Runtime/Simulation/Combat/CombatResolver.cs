using System;

namespace HEXBOUND.Simulation
{
    /// <summary>Finds every hit this tick, then applies them all at once so trades are fair.</summary>
    public static class CombatResolver
    {
        private struct PendingHit
        {
            public int Attacker;
            public int Defender;
            public int Damage;
            public int Hitstun;
            public int Blockstun;
            public Fix KnockbackH;
            public Fix KnockbackV;
            public HitFlags Flags;
            public FixVec3 Source;
            /// <summary>Where the attack comes from, for the block arc (the attacker, or the projectile).</summary>
            public FixVec3 ArcSource;
            public int Direction;
            public bool FromProjectile;
            public int ProjectileSlot;
            /// <summary>Overhead or air attack: goes through the low guard.</summary>
            public bool Overhead;
        }

        private const int MaxPending = 8;

        public static void Resolve(GameState s, EventBuffer ev)
        {
            Span<PendingHit> pending = stackalloc PendingHit[MaxPending];
            int count = 0;

            for (int a = 0; a < 2; a++)
            {
                int d = 1 - a;
                ref FighterState attacker = ref s.Fighters[a];
                if (attacker.Action != FighterAction.Attack || attacker.HitStop > 0) continue;
                MoveDef m = GameContent.GetMove(attacker.MoveId);
                if (m == null) continue;
                int t = attacker.MoveTick;
                ref FighterState defender = ref s.Fighters[d];

                for (int i = 0; i < m.Hitboxes.Length && count < MaxPending; i++)
                {
                    HitboxDef hb = m.Hitboxes[i];
                    if (t < hb.Start || t > hb.End) continue;
                    StrikePillars(s, a, ref attacker, hb, ev);
                    StrikeStructures(s, a, ref attacker, hb, ev);
                    int bit = 1 << hb.Group;
                    if (hb.RehitInterval > 0)
                    {
                        if (attacker.RehitTimer > 0) continue;
                    }
                    else if ((attacker.HitMask & bit) != 0)
                    {
                        continue;
                    }

                    FixVec3 center = attacker.Position + FixVec3.FromAngle(attacker.Facing) * hb.Forward + FixVec3.Up * hb.Up;
                    if (!CanBeHit(ref defender, hb.Flags)) continue;
                    if (Ducked(ref defender, center, hb.Flags)) continue;
                    if (!Overlaps(center, hb.Radius, ref defender)) continue;

                    if (hb.RehitInterval > 0) attacker.RehitTimer = hb.RehitInterval;
                    else attacker.HitMask |= bit;

                    pending[count++] = new PendingHit
                    {
                        Attacker = a,
                        Defender = d,
                        Damage = ScaleDamage(s, ref attacker, hb.Damage),
                        Hitstun = hb.Hitstun,
                        Blockstun = hb.Blockstun,
                        KnockbackH = hb.KnockbackH,
                        KnockbackV = hb.KnockbackV,
                        Flags = hb.Flags,
                        Source = (hb.Flags & HitFlags.Radial) != 0 ? attacker.Position : center,
                        ArcSource = attacker.Position,
                        Direction = attacker.Facing,
                        ProjectileSlot = -1,
                        Overhead = (hb.Flags & HitFlags.Overhead) != 0 || !attacker.Grounded || m.Pose == MovePose.Overhead || m.Pose == MovePose.Slam,
                    };
                    break;
                }
            }

            for (int i = 0; i < s.Projectiles.Length && count < MaxPending; i++)
            {
                ref ProjectileState p = ref s.Projectiles[i];
                if (!p.Active) continue;
                int d = 1 - p.Owner;
                ref FighterState defender = ref s.Fighters[d];
                ProjectileDef def = GameContent.Projectiles[p.DefId];
                if (!CanBeHit(ref defender, def.Flags)) continue;
                if (Ducked(ref defender, p.Position, def.Flags)) continue;
                if (!Overlaps(p.Position, def.Radius, ref defender)) continue;

                pending[count++] = new PendingHit
                {
                    Attacker = p.Owner,
                    Defender = d,
                    Damage = ScaleDamage(s, ref s.Fighters[p.Owner], def.Damage),
                    Hitstun = def.Hitstun,
                    Blockstun = def.Blockstun,
                    KnockbackH = def.KnockbackH,
                    KnockbackV = def.KnockbackV,
                    Flags = def.Flags,
                    Source = p.Position,
                    ArcSource = p.Position,
                    Direction = p.Velocity.ToAngle(),
                    FromProjectile = true,
                    ProjectileSlot = i,
                };
                p.Active = false;
                if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileHit, p.Owner, p.SerialId, p.Position);
                if (def.ExplodeRadius > Fix.Zero && ev != null) ev.Add(s.Tick, SimEventType.Explosion, p.Owner, p.DefId, p.Position);
            }

            // Missile blasts: hurt the opponent inside the radius. The owner is never hurt by its own blast.
            for (int i = 0; i < s.Projectiles.Length && count < MaxPending; i++)
            {
                ref ProjectileState p = ref s.Projectiles[i];
                if (!p.Exploding) continue;
                p.Exploding = false;
                int d = 1 - p.Owner;
                ref FighterState defender = ref s.Fighters[d];
                ProjectileDef def = GameContent.Projectiles[p.DefId];
                if (!CanBeHit(ref defender, HitFlags.Radial)) continue;
                if (!Overlaps(p.Position, def.ExplodeRadius, ref defender)) continue;
                // Walls, roofs and bridge decks shield from the blast (Missile Rain does not reach under a roof).
                CharacterDef dc = GameContent.GetCharacter(defender.CharacterId);
                if (!Arena.ClearLine(p.Position, defender.Position + FixVec3.Up * (dc.Height * Fix.Half))) continue;
                pending[count++] = new PendingHit
                {
                    Attacker = p.Owner,
                    Defender = d,
                    Damage = ScaleDamage(s, ref s.Fighters[p.Owner], def.ExplodeDamage),
                    Hitstun = def.ExplodeHitstun,
                    Blockstun = 14,
                    KnockbackH = def.ExplodeKnockbackH,
                    KnockbackV = def.ExplodeKnockbackV,
                    Flags = HitFlags.Radial | HitFlags.Launch,
                    Source = p.Position,
                    ArcSource = p.Position,
                    Direction = 0,
                    FromProjectile = true,
                    ProjectileSlot = i,
                };
            }

            for (int i = 0; i < count; i++)
            {
                Apply(s, ref pending[i], ev);
            }
        }

        /// <summary>A melee hitbox chips any standing breakable pillar it touches, once per hitbox group per move.</summary>
        private static void StrikePillars(GameState s, int a, ref FighterState attacker, HitboxDef hb, EventBuffer ev)
        {
            if (Arena.Layout(s.Setup.Map).PillarHealth <= 0) return;
            FixVec3 center = attacker.Position + FixVec3.FromAngle(attacker.Facing) * hb.Forward + FixVec3.Up * hb.Up;
            int pillar = Arena.PillarAt(center, hb.Radius);
            if (pillar < 0) return;
            int bit = 1 << (pillar * 8 + (hb.Group & 7));
            if ((attacker.PillarHitMask & bit) != 0) return;
            attacker.PillarHitMask |= bit;
            DamagePillar(s, pillar, hb.Damage, a, ev);
        }

        /// <summary>A melee hitbox chips a breakable structure it touches, once per hitbox group per move.</summary>
        private static void StrikeStructures(GameState s, int a, ref FighterState attacker, HitboxDef hb, EventBuffer ev)
        {
            ArenaLayout layout = Arena.Layout(s.Setup.Map);
            if (layout.StructureHealth <= 0) return;
            FixVec3 center = attacker.Position + FixVec3.FromAngle(attacker.Facing) * hb.Forward + FixVec3.Up * hb.Up;
            int g = Arena.StructureNear(center, hb.Radius);
            if (g < 0) return;
            int bit = 1 << (g * 8 + (hb.Group & 7));
            if ((attacker.StructureHitMask & bit) != 0) return;
            attacker.StructureHitMask |= bit;
            DamageStructure(s, g, hb.Damage, a, ev);
        }

        /// <summary>
        /// Takes health from a breakable structure. At 0 it collapses: its blocks disappear, fighters on top fall, and
        /// any fighter underneath its roof or deck is crushed (knocked out).
        /// </summary>
        public static void DamageStructure(GameState s, int group, int damage, int attacker, EventBuffer ev)
        {
            if (group < 0 || group >= s.StructureHealth.Length || s.StructureHealth[group] <= 0 || damage <= 0) return;
            s.StructureHealth[group] -= damage;
            FixVec3 at = Arena.StructureCenter(group);
            if (s.StructureHealth[group] > 0)
            {
                if (ev != null) ev.Add(s.Tick, SimEventType.StructureHit, attacker, group, at);
                return;
            }
            s.StructureHealth[group] = 0;
            if (ev != null) ev.Add(s.Tick, SimEventType.StructureBreak, attacker, group, at);
            // Who is under the roof or deck is found before the blocks vanish.
            bool under0 = Arena.IsUnderStructure(group, s.Fighters[0].Position);
            bool under1 = Arena.IsUnderStructure(group, s.Fighters[1].Position);
            Arena.SetBrokenStructures(Arena.BrokenStructureMask(s));
            for (int i = 0; i < 2; i++)
            {
                ref FighterState f = ref s.Fighters[i];
                if (!(i == 0 ? under0 : under1)) continue;
                if (f.Action == FighterAction.KO || f.Action == FighterAction.RingOut) continue;
                if (ev != null) ev.Add(s.Tick, SimEventType.Crushed, i, group, f.Position);
                DealDamage(s, ref s.Fighters[1 - i], ref f, i, f.Health, ev);
                f.Velocity = FixVec3.Zero;
            }
            for (int i = 0; i < 2; i++) s.Fighters[i].Grounded = false;
        }

        /// <summary>Takes health from a breakable pillar; at 0 it breaks and stops blocking.</summary>
        public static void DamagePillar(GameState s, int pillar, int damage, int attacker, EventBuffer ev)
        {
            if (pillar < 0 || pillar >= s.PillarHealth.Length || s.PillarHealth[pillar] <= 0 || damage <= 0) return;
            s.PillarHealth[pillar] -= damage;
            FixVec3 at = Arena.Pillars[pillar] + FixVec3.Up * Fix.FromInt(1);
            if (s.PillarHealth[pillar] <= 0)
            {
                s.PillarHealth[pillar] = 0;
                Arena.SetBroken(Arena.BrokenMask(s));
                if (ev != null) ev.Add(s.Tick, SimEventType.PillarBreak, attacker, pillar, at);
            }
            else if (ev != null)
            {
                ev.Add(s.Tick, SimEventType.PillarHit, attacker, pillar, at);
            }
        }

        private static bool CanBeHit(ref FighterState f, HitFlags flags)
        {
            if (FighterLogic.IsInvulnerable(ref f)) return false;
            if ((flags & HitFlags.Grab) != 0)
            {
                if (!f.Grounded) return false;
                switch (f.Action)
                {
                    case FighterAction.HitStun:
                    case FighterAction.Launched:
                    case FighterAction.BlockStun:
                        return false;
                }
            }
            return true;
        }

        /// <summary>A crouching fighter is missed by attacks centered high on its body (grabs still connect).</summary>
        private static bool Ducked(ref FighterState f, FixVec3 center, HitFlags flags)
        {
            if ((flags & HitFlags.Grab) != 0 || !FighterLogic.IsCrouched(ref f)) return false;
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            return center.Y > f.Position.Y + c.Height * GameConfig.CrouchDuckPercent / 100;
        }

        /// <summary>Beast mode scales the beast's damage with its level; Awakened hits deal extra damage.</summary>
        private static int ScaleDamage(GameState s, ref FighterState attacker, int damage)
        {
            CharacterDef c = GameContent.GetCharacter(attacker.CharacterId);
            if (s.Setup.Mode == MatchMode.Beast && c.Stance == FighterStance.Beast)
            {
                int level = s.Setup.BeastLevel < 0 ? 0 : (s.Setup.BeastLevel > 10 ? 10 : s.Setup.BeastLevel);
                damage = damage * (75 + 4 * level) / 100;
            }
            if (attacker.Buff == BuffId.Awakened) damage = damage * c.AwakenDamagePercent / 100;
            return damage;
        }

        /// <summary>Sphere against the fighter's vertical capsule hurtbox.</summary>
        private static bool Overlaps(FixVec3 center, Fix radius, ref FighterState f)
        {
            CharacterDef c = GameContent.GetCharacter(f.CharacterId);
            Fix bottom = f.Position.Y + c.Radius;
            Fix top = f.Position.Y + c.Height - c.Radius;
            if (f.Action == FighterAction.Knockdown || f.Action == FighterAction.KO) top = bottom;
            Fix cy = Fix.Clamp(center.Y, bottom, top);
            Fix dx = center.X - f.Position.X;
            Fix dy = center.Y - cy;
            Fix dz = center.Z - f.Position.Z;
            Fix r = radius + c.Radius;
            return dx * dx + dy * dy + dz * dz <= r * r;
        }

        private static bool InBlockArc(ref FighterState defender, FixVec3 source)
        {
            FixVec3 to = (source - defender.Position).Horizontal();
            if (to.LengthSquared() == Fix.Zero) return true;
            return FixMath.AbsAngleDelta(defender.Facing, to.ToAngle()) <= GameConfig.BlockArc;
        }

        private static void Apply(GameState s, ref PendingHit hit, EventBuffer ev)
        {
            ref FighterState a = ref s.Fighters[hit.Attacker];
            ref FighterState d = ref s.Fighters[hit.Defender];
            if (d.Action == FighterAction.KO || d.Action == FighterAction.RingOut) return;
            CharacterDef dc = GameContent.GetCharacter(d.CharacterId);

            bool blockable = (hit.Flags & (HitFlags.Unblockable | HitFlags.Grab)) == 0;
            int stop = hit.Damage / 25 + 3;
            if (stop > 10) stop = 10;

            d.LowGuardStun = false;
            bool standingBlock = d.Action == FighterAction.Block && (hit.Flags & HitFlags.Low) == 0;
            bool lowGuard = d.Action == FighterAction.CrouchBlock && !hit.Overhead;
            if (blockable && (standingBlock || lowGuard) && InBlockArc(ref d, hit.ArcSource))
            {
                if (standingBlock && d.BlockTicks <= GameConfig.ParryWindow)
                {
                    if (ev != null) ev.Add(s.Tick, SimEventType.Parried, hit.Defender, hit.Attacker, d.Position + FixVec3.Up);
                    d.HitStop = 6;
                    if (!hit.FromProjectile)
                    {
                        a.Action = FighterAction.Parried;
                        a.ActionTicks = 0;
                        a.StunTicks = GameConfig.ParriedStun;
                        a.MoveId = -1;
                        a.Velocity.X = Fix.Zero;
                        a.Velocity.Z = Fix.Zero;
                        a.HitStop = 6;
                    }
                    return;
                }

                int chip = hit.Damage * GameConfig.ChipPercent / 100;
                FixVec3 push = PushDirection(ref d, ref hit) * (Fix.Abs(hit.KnockbackH) * Fix.Half + Fix.Milli(40));
                if (lowGuard)
                {
                    // The low guard is never broken, but heavy hits wear it down faster.
                    if ((hit.Flags & HitFlags.GuardBreak) != 0) chip = chip * 2;
                    d.Action = FighterAction.BlockStun;
                    d.ActionTicks = 0;
                    d.StunTicks = hit.Blockstun;
                    d.LowGuardStun = true;
                    if (ev != null) ev.Add(s.Tick, SimEventType.Blocked, hit.Defender, chip, d.Position + FixVec3.Up * Fix.Half);
                    stop = 4;
                }
                else if ((hit.Flags & HitFlags.GuardBreak) != 0)
                {
                    chip = chip * 2;
                    d.Action = FighterAction.GuardBroken;
                    d.ActionTicks = 0;
                    d.StunTicks = GameConfig.GuardBrokenStun;
                    if (ev != null) ev.Add(s.Tick, SimEventType.GuardBreak, hit.Defender, chip, d.Position + FixVec3.Up);
                    stop = 8;
                }
                else
                {
                    d.Action = FighterAction.BlockStun;
                    d.ActionTicks = 0;
                    d.StunTicks = hit.Blockstun;
                    if (ev != null) ev.Add(s.Tick, SimEventType.Blocked, hit.Defender, chip, d.Position + FixVec3.Up);
                    stop = 4;
                }
                d.Velocity.X = push.X;
                d.Velocity.Z = push.Z;
                DealDamage(s, ref a, ref d, hit.Defender, chip, ev);
                d.HitStop = stop;
                if (!hit.FromProjectile) a.HitStop = stop;
                return;
            }

            int scale = 100 - GameConfig.ComboScalingStep * d.ComboCount;
            if (scale < GameConfig.ComboScalingMin) scale = GameConfig.ComboScalingMin;
            int damage = hit.Damage * scale / 100;
            if (damage < 1 && hit.Damage > 0) damage = 1;

            FixVec3 dir = PushDirection(ref d, ref hit);
            Fix kbH = hit.KnockbackH * dc.KnockbackScale;
            Fix kbV = hit.KnockbackV * dc.KnockbackScale;
            d.ComboCount++;
            d.ComboDamage += damage;
            d.MoveId = -1;
            d.BlockTicks = 0;
            d.Velocity = new FixVec3(dir.X * kbH, kbV, dir.Z * kbH);

            bool launch = (hit.Flags & HitFlags.Launch) != 0 || !d.Grounded || kbV > Fix.Zero;
            bool grab = (hit.Flags & HitFlags.Grab) != 0;
            d.Action = launch ? FighterAction.Launched : FighterAction.HitStun;
            d.ActionTicks = 0;
            d.StunTicks = hit.Hitstun;
            if (launch)
            {
                d.Grounded = false;
                d.JuggleCount++;
                if (d.Velocity.Y < Fix.Milli(80)) d.Velocity.Y = Fix.Milli(80);
            }

            if (ev != null) ev.Add(s.Tick, grab ? SimEventType.GrabHit : SimEventType.Hit, hit.Defender, damage, d.Position + FixVec3.Up);
            DealDamage(s, ref a, ref d, hit.Defender, damage, ev);
            d.HitStop = stop;
            if (!hit.FromProjectile) a.HitStop = stop;
            if (hit.Defender == 0) s.QuietTicks0 = 0;
            else s.QuietTicks1 = 0;
        }

        private static FixVec3 PushDirection(ref FighterState d, ref PendingHit hit)
        {
            if ((hit.Flags & HitFlags.Radial) != 0 || hit.FromProjectile)
            {
                FixVec3 away = (d.Position - hit.Source).Horizontal();
                if (away.LengthSquared() > Fix.Milli(1))
                {
                    if (hit.FromProjectile && (hit.Flags & HitFlags.Radial) == 0) return FixVec3.FromAngle(hit.Direction);
                    return away.Normalized();
                }
            }
            return FixVec3.FromAngle(hit.Direction);
        }

        private static void DealDamage(GameState s, ref FighterState a, ref FighterState d, int defenderIndex, int damage, EventBuffer ev)
        {
            d.Health -= damage;
            a.DamageDealt += damage;
            if (d.Health > 0) return;
            d.Health = 0;
            if (s.Setup.Mode == MatchMode.Training)
            {
                d.Health = 1;
                return;
            }
            d.Action = FighterAction.KO;
            d.ActionTicks = 0;
            d.MoveId = -1;
            d.Grounded = false;
            d.Velocity = new FixVec3(d.Velocity.X + d.Velocity.X / 2, Fix.Max(d.Velocity.Y, Fix.Milli(250)), d.Velocity.Z + d.Velocity.Z / 2);
            if (ev != null) ev.Add(s.Tick, SimEventType.KO, defenderIndex, 0, d.Position);
        }
    }
}
