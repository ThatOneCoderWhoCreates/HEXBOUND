namespace HEXBOUND.Simulation
{
    /// <summary>
    /// Xorshift32 random number generator. The state is a plain uint stored in GameState,
    /// so it is saved and restored together with everything else during rollback.
    /// </summary>
    public static class DeterministicRandom
    {
        public static uint Next(ref uint state)
        {
            if (state == 0) state = 0x9E3779B9u;
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Random integer in [min, max).</summary>
        public static int Range(ref uint state, int min, int max)
        {
            if (max <= min) return min;
            uint span = (uint)(max - min);
            return min + (int)(Next(ref state) % span);
        }
    }
}
