namespace Unbounded.Vehicle
{
    /// <summary>
    /// Normalised driver input. Handbrake, drift, and nitro are not part of
    /// this milestone; they will extend this struct when they land.
    /// </summary>
    public struct HandlingInput
    {
        /// <summary>0 to 1.</summary>
        public float Throttle;

        /// <summary>0 to 1. Held at a standstill, this becomes reverse.</summary>
        public float Brake;

        /// <summary>-1 (left) to 1 (right).</summary>
        public float Steer;
    }
}
