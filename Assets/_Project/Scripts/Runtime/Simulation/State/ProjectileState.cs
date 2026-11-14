namespace HEXBOUND.Simulation
{
    public struct ProjectileState
    {
        public bool Active;
        public int Owner;
        public int DefId;
        public FixVec3 Position;
        public FixVec3 Velocity;
        public int TicksLeft;
        /// <summary>Unique id per match so presentation can track each projectile.</summary>
        public int SerialId;
        /// <summary>The projectile just exploded this tick; its blast is resolved by CombatResolver, then cleared.</summary>
        public bool Exploding;
    }
}
