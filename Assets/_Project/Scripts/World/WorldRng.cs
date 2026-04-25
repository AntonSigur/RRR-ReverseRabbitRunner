using System;

namespace ReverseRabbitRunner.World
{
    /// <summary>
    /// Deterministic, scoped pseudo-random source for world generation.
    ///
    /// Backed by <see cref="System.Random"/> so it is independent of
    /// <see cref="UnityEngine.Random"/> — non-world systems (audio,
    /// cosmetic FX) can keep using the global PRNG without polluting the
    /// world-layout sequence. This guarantees that two runs initialised
    /// with the same seed produce the same chunk / obstacle / pickup
    /// layout (provided WorldRng is the only RNG consulted by the
    /// generator).
    ///
    /// Thread-safe is **not** required — Unity calls all generators on
    /// the main thread.
    /// </summary>
    public static class WorldRng
    {
        private static System.Random rng = new System.Random();

        /// <summary>The seed currently driving the generator (informational).</summary>
        public static int CurrentSeed { get; private set; }

        /// <summary>(Re)initialise with the given seed. Call before any world spawning.</summary>
        public static void InitState(int seed)
        {
            CurrentSeed = seed;
            rng = new System.Random(seed);
        }

        /// <summary>Initialise from a non-deterministic source.</summary>
        public static void InitFromTime()
        {
            int seed = unchecked((int)(DateTime.UtcNow.Ticks & 0x7fffffff));
            InitState(seed);
        }

        /// <summary>Returns an int in [minInclusive, maxExclusive).</summary>
        public static int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return rng.Next(minInclusive, maxExclusive);
        }

        /// <summary>Returns a float in [minInclusive, maxInclusive].</summary>
        public static float Range(float minInclusive, float maxInclusive)
        {
            return minInclusive + (float)rng.NextDouble() * (maxInclusive - minInclusive);
        }

        /// <summary>Returns a float in [0, 1].</summary>
        public static float Value => (float)rng.NextDouble();
    }
}
