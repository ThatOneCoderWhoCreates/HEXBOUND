using System;

namespace HEXBOUND.Simulation
{
    [Flags]
    public enum HitFlags
    {
        None = 0,
        /// <summary>Sends the target airborne (juggle state).</summary>
        Launch = 1 << 0,
        /// <summary>Breaks a normal block (not a parry).</summary>
        GuardBreak = 1 << 1,
        /// <summary>Grab: ignores block and parry, misses airborne targets.</summary>
        Grab = 1 << 2,
        /// <summary>Cannot be blocked or parried.</summary>
        Unblockable = 1 << 3,
        /// <summary>Knockback points away from the attacker's position instead of along its facing.</summary>
        Radial = 1 << 4,
    }

    public enum CooldownSlot
    {
        None,
        Skill1,
        Skill2,
        Ultimate,
        Recharge,
        Dodge,
    }

    public enum BuffId : byte
    {
        None = 0,
        TwinBlades = 1,
        Levitate = 2,
    }

    /// <summary>A sphere-shaped attack area, active for a range of ticks of the move.</summary>
    public sealed class HitboxDef
    {
        public int Start;
        public int End;
        public Fix Forward;
        public Fix Up = Fix.One;
        public Fix Radius = Fix.One;
        public int Damage;
        public int Hitstun = 18;
        public int Blockstun = 12;
        /// <summary>Horizontal knockback speed in units per tick. Negative pulls the target in.</summary>
        public Fix KnockbackH;
        /// <summary>Vertical knockback speed in units per tick.</summary>
        public Fix KnockbackV;
        public HitFlags Flags;
        /// <summary>Hitboxes in the same group hit a target at most once (unless RehitInterval is set).</summary>
        public int Group;
        /// <summary>If above zero, this hitbox can hit again after this many ticks (multi-hit).</summary>
        public int RehitInterval;
    }

    /// <summary>Forced movement during a move, in units per tick along the fighter's facing.</summary>
    public sealed class MotionDef
    {
        public int Start;
        public int End;
        public Fix Forward;
        public Fix Up;
        /// <summary>When true, vertical velocity is set to Up during this window (ignores gravity).</summary>
        public bool SetVertical;
    }

    public sealed class ProjectileSpawn
    {
        public int Tick;
        public int ProjectileId;
        /// <summary>Angle offset from facing, in simulation angle units.</summary>
        public int AngleOffset;
    }

    public sealed class ProjectileDef
    {
        public int Id;
        public string Name;
        public Fix Speed;
        public int Lifetime;
        public Fix Radius = Fix.Milli(500);
        public Fix SpawnForward = Fix.One;
        public Fix SpawnUp = Fix.Milli(1100);
        public int Damage;
        public int Hitstun = 18;
        public int Blockstun = 12;
        public Fix KnockbackH;
        public Fix KnockbackV;
        public HitFlags Flags;
        /// <summary>Aim at the opponent's chest in 3D if they are inside the soft target cone.</summary>
        public bool AimAtTarget;
        /// <summary>Gravity applied per tick (zero for straight shots).</summary>
        public Fix Gravity;
        /// <summary>Visual style id for presentation.</summary>
        public int VisualId;
    }

    public sealed class MoveDef
    {
        public int Id;
        public string Name;
        public int Total;
        public HitboxDef[] Hitboxes = Array.Empty<HitboxDef>();
        public MotionDef[] Motions = Array.Empty<MotionDef>();
        public ProjectileSpawn[] Spawns = Array.Empty<ProjectileSpawn>();

        /// <summary>Window in which the move can be cancelled into another action. -1 means no window.</summary>
        public int CancelStart = -1;
        public int CancelEnd = -1;

        /// <summary>Ticks in which the fighter cannot be hit. -1 means none.</summary>
        public int InvulnStart = -1;
        public int InvulnEnd = -1;

        public int MagicCost;
        public int Cooldown;
        public CooldownSlot Slot = CooldownSlot.None;

        public BuffId ApplyBuff = BuffId.None;
        public int BuffDuration;
        public int BuffTick;

        /// <summary>Gives the fighter magic at MagicGainTick (used by the recharge move).</summary>
        public int MagicGain;
        public int MagicGainTick;

        /// <summary>Teleport distance along the move direction at TeleportTick. Zero means no teleport.</summary>
        public Fix TeleportDistance;
        public int TeleportTick;

        public bool UsableOnGround = true;
        public bool UsableInAir;
        /// <summary>The move ends when the fighter lands and plays LandingRecovery ticks of recovery.</summary>
        public bool EndsOnLanding;
        public int LandingRecovery = 8;
        /// <summary>Multiplier for gravity while the move runs (air moves hang in the air).</summary>
        public Fix GravityScale = Fix.One;
        /// <summary>The fighter can steer with the movement input at SteerSpeed (units per tick).</summary>
        public bool Steerable;
        public Fix SteerSpeed;
        /// <summary>Turn toward a nearby opponent when the move starts.</summary>
        public bool SoftTarget = true;
        /// <summary>Visual pose family for presentation (swing, thrust, slam, cast, spin...).</summary>
        public MovePose Pose = MovePose.Swing;
    }

    public enum MovePose : byte
    {
        Swing,
        SwingReverse,
        Thrust,
        Overhead,
        Uppercut,
        Spin,
        Slam,
        Cast,
        Channel,
        Grab,
        Dodge,
        Flurry,
    }
}
