namespace Unbounded.Vehicle
{
    /// <summary>
    /// Normalised driver input. Handbrake and drift are not part of this
    /// milestone; they will extend this struct when they land.
    /// </summary>
    public struct HandlingInput
    {
        /// <summary>0 to 1.</summary>
        public float Throttle;

        /// <summary>0 to 1. Held at a standstill, this becomes reverse.</summary>
        public float Brake;

        /// <summary>-1 (left) to 1 (right).</summary>
        public float Steer;

        /// <summary>Whether Nitro is being asked for. Ignored when the tank is empty or locked out.</summary>
        public bool Nitro;
    }
}
