namespace HEXBOUND.Simulation
{
    public static class ProjectileLogic
    {
        private static readonly Fix ChestRatio = Fix.Milli(550);

        public static void Spawn(GameState s, int owner, ProjectileSpawn spawn, ref FighterState opponent, EventBuffer ev)
        {
            if (spawn.ProjectileId < 0 || spawn.ProjectileId >= GameContent.Projectiles.Length) return;
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
            if (slot < 0) return;

            ref FighterState f = ref s.Fighters[owner];
            int angle = FixMath.NormalizeAngle(f.Facing + spawn.AngleOffset);
            FixVec3 forward = FixVec3.FromAngle(angle);
            FixVec3 origin = f.Position + forward * def.SpawnForward + FixVec3.Up * def.SpawnUp;
            FixVec3 dir = forward;

            if (def.AimAtTarget && opponent.Action != FighterAction.KO && opponent.Action != FighterAction.RingOut)
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
            p.TicksLeft = def.Lifetime;
            p.SerialId = s.NextProjectileSerial++;
            if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileSpawn, owner, def.Id, origin);
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
                bool hitFloor = p.Position.Y < Fix.Zero && Arena.IsInside(p.Position);
                if (p.TicksLeft <= 0 || hitFloor || p.Position.Y < GameConfig.RingOutHeight)
                {
                    p.Active = false;
                    if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileExpire, p.Owner, p.SerialId, p.Position);
                }
            }

            // Projectiles from different owners destroy each other.
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
                        a.Active = false;
                        b.Active = false;
                        if (ev != null) ev.Add(s.Tick, SimEventType.ProjectileClash, a.Owner, a.SerialId, a.Position);
                        break;
                    }
                }
            }
        }
    }
}
