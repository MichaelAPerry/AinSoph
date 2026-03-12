using System;
using AinSoph.Data;

namespace AinSoph.NPC
{
    /// <summary>
    /// Rolls birth impairment for each primitive at NPC creation.
    /// Rates from SKILLS.md — hardcoded to real-world natural occurrence.
    ///
    /// | Primitive | Rate per 1,000 births |
    /// |-----------|----------------------|
    /// | Move      | ~2.5 / 1,000         |
    /// | See       | ~0.4 / 1,000         |
    /// | Hear      | ~1.0 / 1,000         |
    /// | Talk      | ~0.1 / 1,000         |
    ///
    /// No disease names. Only the mechanical reality.
    /// </summary>
    public static class BirthImpairment
    {
        private static readonly Random _rng = new();

        // Rates expressed as probability per birth (0.0–1.0)
        private const double RateMove = 0.0025;
        private const double RateSee  = 0.0004;
        private const double RateHear = 0.0010;
        private const double RateTalk = 0.0001;

        /// <summary>
        /// Rolls impairment for all four primitives and writes results into the data object.
        /// Call once at NPC creation; values are saved and never re-rolled.
        /// </summary>
        public static void Roll(NpcSaveData data)
        {
            data.BrokenMove = _rng.NextDouble() < RateMove;
            data.BrokenSee  = _rng.NextDouble() < RateSee;
            data.BrokenHear = _rng.NextDouble() < RateHear;
            data.BrokenTalk = _rng.NextDouble() < RateTalk;
        }

        /// <summary>Returns a human-readable string of any broken primitives, or empty string if none.</summary>
        public static string Describe(NpcSaveData data)
        {
            var broken = new System.Collections.Generic.List<string>();
            if (data.BrokenMove) broken.Add("Move");
            if (data.BrokenSee)  broken.Add("See");
            if (data.BrokenHear) broken.Add("Hear");
            if (data.BrokenTalk) broken.Add("Talk");
            return broken.Count > 0 ? $"[broken: {string.Join(", ", broken)}]" : string.Empty;
        }
    }
}
