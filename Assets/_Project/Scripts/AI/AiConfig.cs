using System;

namespace Unbounded.AI
{
    /// <summary>
    /// Tuning for one opponent. Speeds are metres per second and angles are
    /// degrees. <see cref="Pace"/> is the only value the field varies, so the
    /// opponents drive the same line with small differences in commitment.
    /// </summary>
    [Serializable]
    public struct AiConfig
    {
        /// <summary>The car's own top speed, so the AI never asks for more than it has.</summary>
        public float TopSpeed;

        /// <summary>Fraction of <see cref="TopSpeed"/> the driver commits to; also scales cornering.</summary>
        public float Pace;

        /// <summary>Metres of line sampled ahead at a standstill.</summary>
        public float LookaheadBase;

        /// <summary>Extra metres of lookahead per metre per second of speed.</summary>
        public float LookaheadPerSpeed;

        /// <summary>The heading error that counts as full lock.</summary>
        public float FullLockAngle;

        /// <summary>Cornering acceleration the driver budgets for, below what the car can hold.</summary>
        public float LateralAccelerationBudget;

        /// <summary>Seconds of travel to scan for curvature when braking for a corner.</summary>
        public float BrakingSeconds;

        /// <summary>Spacing of the curvature probes, in metres.</summary>
        public float CurvatureProbe;

        /// <summary>The slowest a corner is ever taken, so a tight hairpin is not a stop.</summary>
        public float MinCornerSpeed;

        /// <summary>How far ahead the player has to be before catch-up applies, in metres.</summary>
        public float CatchUpGap;

        /// <summary>Metres over which catch-up ramps from nothing to its full boost.</summary>
        public float CatchUpRange;

        /// <summary>The most catch-up adds to <see cref="Pace"/>.</summary>
        public float CatchUpBoost;

        /// <summary>Speed below which the car counts as stuck, in metres per second.</summary>
        public float StuckSpeed;

        /// <summary>Seconds crawling before the driver reverses out.</summary>
        public float StuckSeconds;

        /// <summary>Seconds of reverse before trying forwards again.</summary>
        public float ReverseSeconds;

        public static AiConfig Default => new AiConfig
        {
            TopSpeed = 60f,
            Pace = 0.97f,
            LookaheadBase = 5f,
            LookaheadPerSpeed = 0.25f,
            FullLockAngle = 28f,
            LateralAccelerationBudget = 8f,
            BrakingSeconds = 1.6f,
            CurvatureProbe = 8f,
            MinCornerSpeed = 8f,
            CatchUpGap = 80f,
            CatchUpRange = 120f,
            CatchUpBoost = 0.08f,
            StuckSpeed = 1.5f,
            StuckSeconds = 1.5f,
            ReverseSeconds = 1.2f
        };
    }
}
