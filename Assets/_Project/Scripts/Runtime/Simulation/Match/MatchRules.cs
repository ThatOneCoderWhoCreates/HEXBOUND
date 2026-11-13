namespace HEXBOUND.Simulation
{
    /// <summary>Tunable game rules. All time values are in ticks (60 ticks = 1 second).</summary>
    public static class GameConfig
    {
        public const int TickRate = 60;

        public const int MaxMagic = 500;
        public const int StartMagic = 500;

        public const int RoundTimeSeconds = 99;
        public const int WinsNeeded = 2;
        public const int MaxRounds = 5;

        public const int IntroTicks = 120;
        public const int FightCallTick = 80;
        public const int RoundOverTicks = 180;

        /// <summary>A block started this many ticks before a hit counts as a parry.</summary>
        public const int ParryWindow = 8;
        public const int ParriedStun = 40;
        public const int GuardBrokenStun = 50;
        /// <summary>Percent of damage taken through a block.</summary>
        public const int ChipPercent = 10;
        /// <summary>Half angle (simulation units) in front of a blocker that the block covers. 16384 = 90 degrees.</summary>
        public const int BlockArc = 16384;

        public const int KnockdownTicks = 40;
        public const int GetUpTicks = 14;
        public const int GetUpInvuln = 20;

        public const int InputBufferTicks = 10;

        /// <summary>Turn speed toward the movement direction, in simulation angle units per tick.</summary>
        public const int TurnSpeed = 5000;

        /// <summary>Soft targeting range and half angle.</summary>
        public static readonly Fix SoftTargetRange = Fix.FromInt(8);
        public const int SoftTargetArc = 13653;

        /// <summary>Lock-on breaks beyond this distance.</summary>
        public static readonly Fix LockOnRange = Fix.FromInt(30);

        /// <summary>Damage scaling per combo hit (percent), and the minimum.</summary>
        public const int ComboScalingStep = 8;
        public const int ComboScalingMin = 40;

        /// <summary>Extra gravity per juggle hit, to stop infinite air combos.</summary>
        public static readonly Fix JuggleGravityStep = Fix.Milli(2);

        public static readonly Fix GroundFriction = Fix.Milli(820);
        public static readonly Fix AirFriction = Fix.Milli(985);

        /// <summary>A fighter below this height is out of the arena (ring-out).</summary>
        public static readonly Fix RingOutHeight = Fix.FromInt(-8);

        /// <summary>Levitate flight settings.</summary>
        public static readonly Fix FlightMinHeight = Fix.Milli(1200);
        public static readonly Fix FlightMaxHeight = Fix.FromInt(6);
        public static readonly Fix FlightVerticalSpeed = Fix.Milli(110);
        public static readonly Fix FlightSpeed = Fix.Milli(115);

        /// <summary>Training mode refills health after this many ticks without being hit.</summary>
        public const int TrainingRefillDelay = 120;
    }

    public enum MatchMode : byte
    {
        Versus = 0,
        Training = 1,
    }

    public struct MatchSetup
    {
        public int Character0;
        public int Character1;
        public uint Seed;
        public MatchMode Mode;
        /// <summary>Training option: magic and cooldowns refill instantly.</summary>
        public bool InfiniteMagic;
    }
}
