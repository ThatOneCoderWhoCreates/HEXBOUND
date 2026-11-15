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
        /// <summary>Holding C: low stance that ducks high attacks.</summary>
        Crouch,
        /// <summary>Holding C + right mouse: low guard. Blocks low and mid attacks, loses to overheads, air attacks and grabs.</summary>
        CrouchBlock,
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
        /// <summary>Pillars already struck by this move: bit (pillar * 8 + hitbox group).</summary>
        public int PillarHitMask;
        /// <summary>Structures already chipped by the current move (bit = structure * 8 + hitbox group).</summary>
        public int StructureHitMask;
        public int RehitTimer;

        public int CooldownSkill1;
        public int CooldownSkill2;
        public int CooldownUltimate;
        public int CooldownRecharge;
        public int CooldownDodge;
        public int CooldownHeal;
        /// <summary>Ticks until the next shot (Vex's rockets).</summary>
        public int CooldownShot;

        /// <summary>True during blockstun that came from the low guard (keeps the low hurtbox).</summary>
        public bool LowGuardStun;
        /// <summary>The beast's low-health awakening has happened this round.</summary>
        public bool Awakened;
        /// <summary>0 = primary weapon, 1 = secondary weapon (Launcher: missile launcher / pistol).</summary>
        public byte WeaponMode;
        /// <summary>Ticks left in which a light attack continues the combo after the previous combo hit ended.</summary>
        public int ComboGrace;
        /// <summary>Weapons in hand or on the back (bit 0 primary, bit 1 secondary).</summary>
        public byte WeaponsHeld;
        /// <summary>Weapons flying after a throw.</summary>
        public byte WeaponsThrown;
        /// <summary>Weapons lying on the floor, at DropPos0 / DropPos1 (also the last position while flying).</summary>
        public byte WeaponsDropped;
        /// <summary>Dropped weapons the fighter has stepped away from; walking back over them picks them up.</summary>
        public byte PickupReady;
        /// <summary>Ticks G has been held (released: tap drops, long press throws).</summary>
        public byte ThrowHold;
        public bool ThrowCharged;
        public FixVec3 DropPos0;
        public FixVec3 DropPos1;
        /// <summary>Crosshair aim from the input this tick (see InputFrame).</summary>
        public bool HasAim;
        public byte AimYaw;
        public sbyte AimPitch;
        /// <summary>Recoil boosts from downward shots since last on the ground.</summary>
        public byte RecoilJumps;

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
