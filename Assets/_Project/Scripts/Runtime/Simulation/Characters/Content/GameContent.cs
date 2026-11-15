namespace HEXBOUND.Simulation
{
    /// <summary>
    /// All fighters, moves and projectiles, built once in a fixed order so ids are identical on every machine.
    /// </summary>
    public static class GameContent
    {
        public static readonly CharacterDef[] Characters;
        public static readonly MoveDef[] Moves;
        public static readonly ProjectileDef[] Projectiles;

        /// <summary>The first PlayableCount characters can be picked in character select; the rest are mode bosses.</summary>
        public static readonly int PlayableCount;
        /// <summary>Character id of the beast (Fight the Beast mode).</summary>
        public static readonly int BeastId;

        /// <summary>Hash of all gameplay numbers. Peers compare it so both run the same content.</summary>
        public static readonly ulong ContentHash;

        static GameContent()
        {
            var b = new ContentBuilder();
            var common = CommonMoves.Build(b);
            Characters = new[]
            {
                SwordFighter.Build(b, common),
                BoxerFighter.Build(b, common),
                WandFighter.Build(b, common),
                StaffFighter.Build(b, common),
                LauncherFighter.Build(b, common),
                BeastFighter.Build(b, common),
            };
            // Weapon throws (hold G), built after every fighter so earlier ids never change.
            for (int i = 0; i < Characters.Length; i++)
            {
                CharacterDef c = Characters[i];
                if (!c.CanThrowWeapon) continue;
                c.ThrowPrimary = CommonMoves.ThrowMove(b, "Throw " + c.Weapon, CommonMoves.ThrownWeapon(b, "Thrown " + c.Weapon, c.ThrowDamage));
                if (c.SecondaryWeapon != null)
                {
                    c.ThrowSecondary = CommonMoves.ThrowMove(b, "Throw " + c.SecondaryWeapon, CommonMoves.ThrownWeapon(b, "Thrown " + c.SecondaryWeapon, c.ThrowDamageSecondary));
                }
            }
            for (int i = 0; i < Characters.Length; i++)
            {
                Characters[i].Id = i;
                if (Characters[i].Selectable) PlayableCount = i + 1;
                if (Characters[i].Stance == FighterStance.Beast) BeastId = i;
            }
            Moves = b.Moves.ToArray();
            Projectiles = b.Projectiles.ToArray();
            ContentHash = ComputeHash();
        }

        public static CharacterDef GetCharacter(int id)
        {
            if (id < 0 || id >= Characters.Length) return Characters[0];
            return Characters[id];
        }

        public static MoveDef GetMove(int id)
        {
            if (id < 0 || id >= Moves.Length) return null;
            return Moves[id];
        }

        private static ulong ComputeHash()
        {
            var h = new StateHasher();
            for (int i = 0; i < Characters.Length; i++)
            {
                CharacterDef c = Characters[i];
                h.Add(c.MaxHealth);
                h.Add(c.Radius);
                h.Add(c.Height);
                h.Add(c.MoveSpeed);
                h.Add(c.AirSpeed);
                h.Add(c.JumpVelocity);
                h.Add(c.Gravity);
                h.Add(c.KnockbackScale);
                h.Add(c.BuffSpeedScale);
                h.Add(c.MaxMagic);
                h.Add(c.AwakenPercent);
                h.Add(c.AwakenDamagePercent);
                h.Add(c.Name.Length);
            }
            for (int map = 0; map < Arena.MapCount; map++)
            {
                ArenaLayout layout = Arena.Layout(map);
                for (int i = 0; i < Arena.EdgeSamples; i++)
                {
                    h.Add(Arena.EdgeRadius(layout, i * (FixMath.FullTurn / Arena.EdgeSamples)));
                }
                for (int i = 0; i < layout.Pillars.Length; i++)
                {
                    h.Add(layout.Pillars[i]);
                }
                h.Add(layout.PillarRadius);
                h.Add(layout.PillarHeight);
                h.Add(layout.PillarHealth);
                for (int i = 0; i < layout.WallCenters.Length; i++)
                {
                    h.Add(layout.WallCenters[i]);
                    h.Add(layout.WallHalfWidths[i]);
                }
                h.Add(layout.Spawn0);
                h.Add(layout.Spawn1);
            }
            for (int i = 0; i < Moves.Length; i++)
            {
                MoveDef m = Moves[i];
                h.Add(m.Total);
                h.Add(m.ThrowsWeapon ? 1 : 0);
                h.Add(m.CancelStart);
                h.Add(m.CancelEnd);
                h.Add(m.InvulnStart);
                h.Add(m.InvulnEnd);
                h.Add(m.MagicCost);
                h.Add(m.Cooldown);
                h.Add(m.BuffDuration);
                h.Add(m.MagicGain);
                h.Add(m.TeleportDistance);
                h.Add(m.GravityScale);
                h.Add(m.SteerSpeed);
                h.Add(m.HealPercent);
                h.Add(m.Crouching ? 1 : 0);
                foreach (HitboxDef hb in m.Hitboxes)
                {
                    h.Add(hb.Start);
                    h.Add(hb.End);
                    h.Add(hb.Forward);
                    h.Add(hb.Up);
                    h.Add(hb.Radius);
                    h.Add(hb.Damage);
                    h.Add(hb.Hitstun);
                    h.Add(hb.Blockstun);
                    h.Add(hb.KnockbackH);
                    h.Add(hb.KnockbackV);
                    h.Add((int)hb.Flags);
                    h.Add(hb.RehitInterval);
                }
                foreach (MotionDef md in m.Motions)
                {
                    h.Add(md.Start);
                    h.Add(md.End);
                    h.Add(md.Forward);
                    h.Add(md.Up);
                }
            }
            for (int i = 0; i < Projectiles.Length; i++)
            {
                ProjectileDef p = Projectiles[i];
                h.Add(p.Speed);
                h.Add(p.Lifetime);
                h.Add(p.Radius);
                h.Add(p.Damage);
                h.Add(p.KnockbackH);
                h.Add(p.KnockbackV);
                h.Add((int)p.Flags);
                h.Add(p.ExplodeRadius);
                h.Add(p.ExplodeDamage);
                h.Add(p.ExplodeKnockbackH);
                h.Add(p.ExplodeKnockbackV);
                h.Add(p.Downward ? 1 : 0);
                h.Add(p.FromSky ? 1 : 0);
                h.Add(p.SpawnUp);
                h.Add(p.SpawnForward);
                h.Add(p.UseAim ? 1 : 0);
                h.Add(p.RecoilLift);
            }
            return h.Value;
        }
    }
}
