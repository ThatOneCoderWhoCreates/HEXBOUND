namespace HEXBOUND.Simulation
{
    public enum SimEventType : byte
    {
        MoveStart,
        Hit,
        Blocked,
        Parried,
        GuardBreak,
        GrabHit,
        ProjectileSpawn,
        ProjectileHit,
        ProjectileClash,
        ProjectileExpire,
        Jump,
        Land,
        Dodge,
        Teleport,
        BuffStart,
        BuffEnd,
        Recharge,
        KO,
        RingOut,
        RoundIntro,
        RoundFight,
        RoundEnd,
        MatchEnd,
        NotEnoughMagic,
        Heal,
        Awaken,
        /// <summary>A missile exploded (Param = projectile definition id).</summary>
        Explosion,
        WeaponSwitch,
        /// <summary>A breakable pillar was struck (Param = pillar index).</summary>
        PillarHit,
        /// <summary>A breakable pillar broke and fell apart (Param = pillar index).</summary>
        PillarBreak,
    }

    /// <summary>Something that happened during a tick, for visuals and audio. Not part of the game state.</summary>
    public struct SimEvent
    {
        public int Tick;
        public SimEventType Type;
        /// <summary>The fighter that caused the event (0 or 1), or -1.</summary>
        public int Fighter;
        public int Param;
        public FixVec3 Position;

        /// <summary>Stable key used by presentation to avoid playing the same event twice after a rollback.</summary>
        public long Key
        {
            get { return ((long)Tick << 24) ^ ((long)Type << 16) ^ ((long)(Fighter + 1) << 12) ^ (Param & 0xFFF); }
        }
    }

    /// <summary>Fixed-size event list, reused every tick to avoid allocations.</summary>
    public sealed class EventBuffer
    {
        private readonly SimEvent[] _events;
        private int _count;

        public EventBuffer(int capacity = 64)
        {
            _events = new SimEvent[capacity];
        }

        public int Count
        {
            get { return _count; }
        }

        public SimEvent this[int index]
        {
            get { return _events[index]; }
        }

        public void Clear()
        {
            _count = 0;
        }

        public void Add(int tick, SimEventType type, int fighter, int param, FixVec3 position)
        {
            if (_count >= _events.Length) return;
            _events[_count++] = new SimEvent
            {
                Tick = tick,
                Type = type,
                Fighter = fighter,
                Param = param,
                Position = position,
            };
        }
    }
}
