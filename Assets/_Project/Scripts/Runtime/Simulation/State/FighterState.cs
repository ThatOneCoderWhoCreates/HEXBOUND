namespace HEXBOUND.Simulation
{
    public enum FighterAction : byte
    {
        Idle,
        Move,
        Air,
        Attack,
        Block,
        BlockStun,
        HitStun,
        Launched,
        Knockdown,
        GetUp,
        GuardBroken,
        Parried,
        Landing,
        KO,
        RingOut,
        Victory,
    }

    /// <summary>
    /// Everything about one fighter that can change during a match.
    /// Plain value data only (no references), so copying the struct is a full snapshot.
    /// </summary>
    public struct FighterState
    {
        public byte CharacterId;
        public FighterAction Action;
        public int ActionTicks;

        public FixVec3 Position;
        public FixVec3 Velocity;
        public int Facing;
        public bool Grounded;

        public int Health;
        public int Magic;

        /// <summary>Current move id, or -1 when not in a move.</summary>
        public int MoveId;
        public int MoveTick;
        /// <summary>Direction the current move travels in (for dodges and blinks).</summary>
        public int MoveDirection;
        public int ComboStep;
        public int HitMask;
        public int RehitTimer;

        public int CooldownSkill1;
        public int CooldownSkill2;
        public int CooldownUltimate;
        public int CooldownRecharge;
        public int CooldownDodge;

        public BuffId Buff;
        public int BuffTicks;

        /// <summary>Remaining ticks of hitstun, blockstun and similar states.</summary>
        public int StunTicks;
        /// <summary>Freeze frames after a hit lands, for impact feel.</summary>
        public int HitStop;
        /// <summary>Extra invulnerability outside of move invulnerability (for example after getting up).</summary>
        public int InvulnTicks;

        public int ComboCount;
        public int JuggleCount;
        public int ComboDamage;

        public InputButtons PrevButtons;
        public InputButtons BufferedButton;
        public int BufferTicks;

        public bool LockOn;
        public bool AirDashUsed;
        public int BlockTicks;

        /// <summary>Total damage dealt this round (for results).</summary>
        public int DamageDealt;
    }
}
