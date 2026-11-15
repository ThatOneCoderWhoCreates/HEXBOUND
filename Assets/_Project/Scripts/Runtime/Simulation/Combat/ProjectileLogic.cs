namespace HEXBOUND.Simulation
{
    public static class ProjectileLogic
    {
        private static readonly Fix ChestRatio = Fix.Milli(550);

        /// <summary>Converts the input's aim pitch steps into a simulation angle (+ is up), limited to 85 degrees either way.</summary>
        public static int AimPitchAngle(sbyte steps)
        {
            int angle = steps * FixMath.FullTurn * 3 / 720;
            int limit = FixMath.FullTurn * 85 / 360;
            if (angle > limit) angle = limit;
            if (angle < -limit) angle = -limit;
            return angle;
        }

        /// <summary>Aim assist: a crosshair shot this close (in angle) to the opponent's chest goes straight at it.</summary>
        private static readonly int AimAssistAngle = FixMath.FullTurn * 7 / 360;

        /// <summary>Distance an arcing shot is lobbed for when it is not aimed straight at the opponent.</summary>
        private static readonly Fix ArcRange = Fix.FromInt(20);

        /// <summary>Spawns a projectile. Returns its slot, or -1 if none was free.</summary>
        public static int Spawn(GameState s, int owner, ProjectileSpawn spawn, ref FighterState opponent, EventBuffer ev)
        {
            if (spawn.ProjectileId < 0 || spawn.ProjectileId >= GameContent.Projectiles.Length) return -1;
            ProjectileDef def = GameContent.Projectiles[spawn.ProjectileId];

            int slot = -1;
            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                if (!s.Projectiles[i].Active)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) return -1;

            ref FighterState f = ref s.Fighters[owner];
            if (def.UseAim && f.HasAim) f.Facing = f.AimYaw << 8;
            int angle = FixMath.NormalizeAngle(f.Facing + spawn.AngleOffset);
            FixVec3 forward = FixVec3.FromAngle(angle);
            FixVec3 origin = f.Position + forward * def.SpawnForward + FixVec3.Up * def.SpawnUp;
            FixVec3 dir = forward;

            if (def.Downward)
            {
                origin = f.Position + FixVec3.Up * def.SpawnUp;
                dir = new FixVec3(Fix.Zero, -Fix.One, Fix.Zero);
            }
            else if (def.FromSky)
            {
                FixVec3 ring = FixVec3.FromAngle(FixMath.NormalizeAngle(spawn.AngleOffset)) * def.SpawnForward;
                FixVec3 target = opponent.Position;
                target.Y = Fix.Zero;
                origin = target + ring + FixVec3.Up * def.SpawnUp;
                dir = new FixVec3(Fix.Zero, -Fix.One, Fix.Zero);
            }
            else if (def.UseAim && f.HasAim)
            {
                // Fly toward the crosshair: the camera's yaw and pitch.
                int pitch = AimPitchAngle(f.AimPitch);
                Fix flat = FixMath.Cos(pitch);
                dir = new FixVec3(forward.X * flat, FixMath.Sin(pitch), forward.Z * flat);
                if (opponent.Action != FighterAction.KO && opponent.Action != FighterAction.RingOut)
                {
                    CharacterDef oc = GameContent.GetCharacter(opponent.CharacterId);
                    FixVec3 to = opponent.Position + FixVec3.Up * (oc.Height * ChestRatio) - origin;
                    Fix len = to.Length();
                    if (len > Fix.Zero && len <= GameConfig.SoftTargetRange * 4)
                    {
                        FixVec3 toDir = to / len;
                        Fix dot = toDir.X * dir.X + toDir.Y * dir.Y + toDir.Z * dir.Z;
                        if (dot >= FixMath.Cos(AimAssistAngle)) dir = toDir;
                    }
                }
            }
            else if (def.AimAtTarget && opponent.Action != FighterAction.KO && opponent.Action != FighterAction.RingOut)
            {
                CharacterDef oc = GameContent.GetCharacter(opponent.CharacterId);
                FixVec3 chest = opponent.Position + FixVec3.Up * (oc.Height * ChestRatio);
                FixVec3 to = chest - origin;
                Fix range = GameConfig.SoftTargetRange * 2;
                if (to.LengthSquared() <= range * range && FixMath.AbsAngleDelta(f.Facing, to.ToAngle()) <= GameConfig.SoftTargetArc)
                {
                    FixVec3 aimed = to.Normalized();
                    Fix horizontal = aimed.HorizontalLength();
                    FixVec3 flat = FixVec3.FromAngle(FixMath.NormalizeAngle(to.ToAngle() + spawn.AngleOffset)) * horizontal;
                    dir = new FixVec3(flat.X, aimed.Y, flat.Z);
                }
            }

            ref ProjectileState p = ref s.Projectiles[slot];
            p.Active = true;
            p.Owner = owner;
            p.DefId = def.Id;
            p.Position = origin;
            p.Velocity = dir * def.Speed;
            if (def.Gravity > Fix.Zero && !def.Downward && !def.FromSky && dir.Y > -Fix.Milli(500))
            {
                // Lob compensation: add the upward speed that gravity takes away over the flight to the aim point,
                // so the shot arcs up and comes back down on target (20 m away when nothing is targeted).
                Fix range = ArcRange;
                if (opponent.Action != FighterAction.KO && opponent.Action != FighterAction.RingOut)
                {
                    Fix toOpp = (opponent.Position - origin).HorizontalLength();
                    FixVec3 flat = dir.Horizontal();
                    Fix flatLen = flat.HorizontalLength();
                    FixVec3 toO = (opponent.Position - origin).Horizontal();
                    if (flatLen > Fix.Zero && toOpp > Fix.Zero && toOpp < Fix.FromInt(40))
                    {
                        Fix along = (toO.X * flat.X + toO.Z * flat.Z) / (toOpp * flatLen);
                        if (along > Fix.Milli(985)) range = toOpp;
                    }
                }
                Fix horizontalSpeed = (dir * def.Speed).HorizontalLength();
                if (horizontalSpeed > Fix.Milli(50))
                {
                    Fix ticks = range / horizontalSpeed;
                    p.Velocity.Y = p.Velocity.Y + def.Gravity * ticks * Fix.Half;
                }
            }
            p.TicksLeft = def.Lifetime;
            p.SerialId = s.NextProjectileSerial++;
            p.Exploding = false;
            p.Carries = 0;
            if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileSpawn, owner, def.Id, origin);
            return slot;
        }

        /// <summary>Marks the projectile's blast for this tick (CombatResolver hits the opponent if inside the radius).</summary>
        public static void Explode(GameState s, ref ProjectileState p, EventBuffer ev)
        {
            p.Active = false;
            p.Exploding = true;
            if (ev != null) ev.Add(s.Tick, SimEventType.Explosion, p.Owner, p.DefId, p.Position);
            ProjectileDef def = GameContent.Projectiles[p.DefId];
            if (Arena.Layout(s.Setup.Map).PillarHealth <= 0 || def.ExplodeRadius <= Fix.Zero) return;
            // The blast chips every standing pillar inside its radius.
            for (int i = 0; i < Arena.Pillars.Length; i++)
            {
                if (Arena.IsBroken(i)) continue;
                Fix dx = p.Position.X - Arena.Pillars[i].X;
                Fix dz = p.Position.Z - Arena.Pillars[i].Z;
                Fix reach = def.ExplodeRadius + Arena.PillarRadius;
                if (dx * dx + dz * dz < reach * reach && p.Position.Y <= Arena.PillarHeight + def.ExplodeRadius)
                {
                    CombatResolver.DamagePillar(s, i, def.ExplodeDamage, p.Owner, ev);
                }
            }
        }

        public static void Step(GameState s, EventBuffer ev)
        {
            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                ref ProjectileState p = ref s.Projectiles[i];
                if (!p.Active) continue;
                ProjectileDef def = GameContent.Projectiles[p.DefId];
                p.Velocity.Y = p.Velocity.Y - def.Gravity;
                p.Position = p.Position + p.Velocity;
                p.TicksLeft--;
                int pillar = Arena.PillarAt(p.Position, def.Radius);
                bool hitFloor = (p.Position.Y < Fix.Zero && Arena.IsInside(p.Position)) || pillar >= 0;
                if (pillar >= 0 && def.ExplodeRadius <= Fix.Zero && Arena.Layout(s.Setup.Map).PillarHealth > 0) CombatResolver.DamagePillar(s, pillar, def.Damage, p.Owner, ev);
                if (p.TicksLeft <= 0 || hitFloor || p.Position.Y < GameConfig.RingOutHeight)
                {
                    p.Active = false;
                    if (def.ExplodeRadius > Fix.Zero)
                    {
                        if (hitFloor && p.Position.Y < Fix.Zero) p.Position.Y = Fix.Zero;
                        Explode(s, ref p, ev);
                    }
                    else if (ev != null)
                    {
                        ev.Add(s.Tick, SimEventType.ProjectileExpire, p.Owner, p.SerialId, p.Position);
                    }
                }
            }

            // Projectiles from different owners destroy each other (missiles explode).
            for (int i = 0; i < s.Projectiles.Length; i++)
            {
                ref ProjectileState a = ref s.Projectiles[i];
                if (!a.Active) continue;
                for (int j = i + 1; j < s.Projectiles.Length; j++)
                {
                    ref ProjectileState b = ref s.Projectiles[j];
                    if (!b.Active || b.Owner == a.Owner) continue;
                    Fix r = GameContent.Projectiles[a.DefId].Radius + GameContent.Projectiles[b.DefId].Radius;
                    if ((a.Position - b.Position).LengthSquared() <= r * r)
                    {
                        bool aStays = GameContent.Projectiles[a.DefId].Unstoppable;
                        bool bStays = GameContent.Projectiles[b.DefId].Unstoppable;
                        if (aStays != bStays)
                        {
                            // The unstoppable one (golden dragon) swallows the other.
                            if (aStays) b.Active = false;
                            else a.Active = false;
                            if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileClash, a.Owner, a.SerialId, aStays ? b.Position : a.Position);
                            if (!a.Active) break;
                            continue;
                        }
                        a.Active = false;
                        b.Active = false;
                        if (GameContent.Projectiles[a.DefId].ExplodeRadius > Fix.Zero) Explode(s, ref a, ev);
                        if (GameContent.Projectiles[b.DefId].ExplodeRadius > Fix.Zero) Explode(s, ref b, ev);
                        if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileClash, a.Owner, a.SerialId, a.Position);
                        break;
                    }
                }
            }
        }
    }
}
