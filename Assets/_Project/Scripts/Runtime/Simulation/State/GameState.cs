using System;

namespace HEXBOUND.Simulation
{
    public enum MatchPhase : byte
    {
        Intro,
        Fighting,
        RoundOver,
        MatchOver,
    }

    public enum RoundEndReason : byte
    {
        None,
        KO,
        RingOut,
        Timeout,
        DoubleKO,
    }

    /// <summary>
    /// The complete state of a match. Rollback saves and restores copies of this object,
    /// so every value that changes during a match must live here.
    /// </summary>
    public sealed class GameState
    {
        public const int MaxProjectiles = 24;

        public int Tick;
        public MatchPhase Phase;
        public int PhaseTicks;
        public int RoundTicksLeft;
        public int Round;
        public int Wins0;
        public int Wins1;
        /// <summary>Winner of the last round: 0, 1, -1 for none yet, 2 for a draw.</summary>
        public int RoundWinner = -1;
        public RoundEndReason LastRoundEnd;
        /// <summary>Winner of the match: 0, 1, -1 for none yet, 2 for a draw.</summary>
        public int MatchWinner = -1;
        public uint Rng;
        public int NextProjectileSerial;
        public MatchSetup Setup;

        public readonly FighterState[] Fighters = new FighterState[2];
        public readonly ProjectileState[] Projectiles = new ProjectileState[MaxProjectiles];

        /// <summary>Ticks since each fighter was last hit (training refill).</summary>
        public int QuietTicks0;
        public int QuietTicks1;

        public void CopyFrom(GameState other)
        {
            Tick = other.Tick;
            Phase = other.Phase;
            PhaseTicks = other.PhaseTicks;
            RoundTicksLeft = other.RoundTicksLeft;
            Round = other.Round;
            Wins0 = other.Wins0;
            Wins1 = other.Wins1;
            RoundWinner = other.RoundWinner;
            LastRoundEnd = other.LastRoundEnd;
            MatchWinner = other.MatchWinner;
            Rng = other.Rng;
            NextProjectileSerial = other.NextProjectileSerial;
            Setup = other.Setup;
            QuietTicks0 = other.QuietTicks0;
            QuietTicks1 = other.QuietTicks1;
            Array.Copy(other.Fighters, Fighters, Fighters.Length);
            Array.Copy(other.Projectiles, Projectiles, Projectiles.Length);
        }

        public GameState Clone()
        {
            var copy = new GameState();
            copy.CopyFrom(this);
            return copy;
        }

        public int GetWins(int fighter)
        {
            return fighter == 0 ? Wins0 : Wins1;
        }

        /// <summary>Deterministic hash of the whole state, used to detect desyncs between peers.</summary>
        public ulong Checksum()
        {
            var h = new StateHasher();
            h.Add(Tick);
            h.Add((int)Phase);
            h.Add(PhaseTicks);
            h.Add(RoundTicksLeft);
            h.Add(Round);
            h.Add(Wins0);
            h.Add(Wins1);
            h.Add(RoundWinner);
            h.Add((int)LastRoundEnd);
            h.Add(MatchWinner);
            h.Add((int)Rng);
            h.Add(NextProjectileSerial);
            h.Add(QuietTicks0);
            h.Add(QuietTicks1);
            for (int i = 0; i < Fighters.Length; i++)
            {
                AddFighter(ref h, ref Fighters[i]);
            }
            for (int i = 0; i < Projectiles.Length; i++)
            {
                ref ProjectileState p = ref Projectiles[i];
                h.Add(p.Active ? 1 : 0);
                if (!p.Active) continue;
                h.Add(p.Owner);
                h.Add(p.DefId);
                h.Add(p.Position);
                h.Add(p.Velocity);
                h.Add(p.TicksLeft);
                h.Add(p.SerialId);
            }
            return h.Value;
        }

        private static void AddFighter(ref StateHasher h, ref FighterState f)
        {
            h.Add(f.CharacterId);
            h.Add((int)f.Action);
            h.Add(f.ActionTicks);
            h.Add(f.Position);
            h.Add(f.Velocity);
            h.Add(f.Facing);
            h.Add(f.Grounded ? 1 : 0);
            h.Add(f.Health);
            h.Add(f.Magic);
            h.Add(f.MoveId);
            h.Add(f.MoveTick);
            h.Add(f.MoveDirection);
            h.Add(f.ComboStep);
            h.Add(f.HitMask);
            h.Add(f.RehitTimer);
            h.Add(f.CooldownSkill1);
            h.Add(f.CooldownSkill2);
            h.Add(f.CooldownUltimate);
            h.Add(f.CooldownRecharge);
            h.Add(f.CooldownDodge);
            h.Add(f.CooldownHeal);
            h.Add(f.LowGuardStun ? 1 : 0);
            h.Add(f.Awakened ? 1 : 0);
            h.Add(f.WeaponMode);
            h.Add((int)f.Buff);
            h.Add(f.BuffTicks);
            h.Add(f.StunTicks);
            h.Add(f.HitStop);
            h.Add(f.InvulnTicks);
            h.Add(f.ComboCount);
            h.Add(f.JuggleCount);
            h.Add(f.ComboDamage);
            h.Add((int)f.PrevButtons);
            h.Add((int)f.BufferedButton);
            h.Add(f.BufferTicks);
            h.Add(f.LockOn ? 1 : 0);
            h.Add(f.AirDashUsed ? 1 : 0);
            h.Add(f.BlockTicks);
            h.Add(f.DamageDealt);
        }
    }

    /// <summary>64-bit FNV-1a hash over integers.</summary>
    public struct StateHasher
    {
        private ulong _value;
        private bool _started;

        public ulong Value
        {
            get { return _started ? _value : 14695981039346656037UL; }
        }

        public void Add(int value)
        {
            if (!_started)
            {
                _value = 14695981039346656037UL;
                _started = true;
            }
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    _value ^= (byte)(value >> (i * 8));
                    _value *= 1099511628211UL;
                }
            }
        }

        public void Add(long value)
        {
            Add((int)value);
            Add((int)(value >> 32));
        }

        public void Add(Fix value)
        {
            Add(value.Raw);
        }

        public void Add(FixVec3 value)
        {
            Add(value.X);
            Add(value.Y);
            Add(value.Z);
        }
    }
}
