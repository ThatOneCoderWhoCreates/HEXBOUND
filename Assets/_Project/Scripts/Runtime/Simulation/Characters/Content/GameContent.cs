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

        /// <summary>Hash of all gameplay numbers. Peers compare it so both run the same content.</summary>
        public static readonly ulong ContentHash;

        static GameContent()
        {
            var b = new ContentBuilder();
            var common = CommonMoves.Build(b);
            // The Staff fighter (StaffFighter.cs) is kept for later but is not in the roster until it has a model.
            Characters = new[]
            {
                SwordFighter.Build(b, common),
                BoxerFighter.Build(b, common),
                WandFighter.Build(b, common),
            };
            for (int i = 0; i < Characters.Length; i++)
            {
                Characters[i].Id = i;
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
                h.Add(c.Name.Length);
            }
            for (int i = 0; i < Arena.EdgeSamples; i++)
            {
                h.Add(Arena.EdgeRadius(i * (FixMath.FullTurn / Arena.EdgeSamples)));
            }
            for (int i = 0; i < Arena.Pillars.Length; i++)
            {
                h.Add(Arena.Pillars[i]);
            }
            h.Add(Arena.PillarRadius);
            h.Add(Arena.PillarHeight);
            for (int i = 0; i < Moves.Length; i++)
            {
                MoveDef m = Moves[i];
                h.Add(m.Total);
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
            }
            return h.Value;
        }
    }
}
