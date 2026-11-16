namespace Unbounded.AI
{
    /// <summary>
    /// One step of driver intent, in the same normalised form a human gives the
    /// car. The scene adapter maps it onto the shared handling model.
    /// </summary>
    public struct AiControls
    {
        /// <summary>0 to 1.</summary>
        public float Throttle;

        /// <summary>0 to 1. Held at a standstill this becomes reverse.</summary>
        public float Brake;

        /// <summary>-1 (left) to 1 (right).</summary>
        public float Steer;

        public static AiControls Neutral => new AiControls();
    }
}
