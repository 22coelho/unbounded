namespace Unbounded.Race
{
    /// <summary>
    /// The fastest Lap the player has driven, carried across sessions. It only
    /// ever moves down, and only an explicit Clear empties it, so restarting a
    /// Race leaves the player's target intact.
    /// </summary>
    public sealed class BestLapRecord
    {
        public BestLapRecord(float storedTime = 0f)
        {
            Time = IsValid(storedTime) ? storedTime : 0f;
        }

        /// <summary>Seconds for the best Lap, or zero when none has been driven.</summary>
        public float Time { get; private set; }

        /// <summary>Whether a best Lap has been recorded.</summary>
        public bool HasTime => Time > 0f;

        /// <summary>
        /// Offers a completed Lap. Returns true when it beats the record and was
        /// stored, so the caller knows to persist the new time.
        /// </summary>
        public bool TryRecord(float lapTime)
        {
            if (!IsValid(lapTime))
            {
                return false;
            }

            if (HasTime && lapTime >= Time)
            {
                return false;
            }

            Time = lapTime;
            return true;
        }

        /// <summary>Forgets the record, for a deliberate reset. Returns true if there was one.</summary>
        public bool Clear()
        {
            if (!HasTime)
            {
                return false;
            }

            Time = 0f;
            return true;
        }

        private static bool IsValid(float lapTime) =>
            lapTime > 0f && !float.IsNaN(lapTime) && !float.IsInfinity(lapTime);
    }
}
