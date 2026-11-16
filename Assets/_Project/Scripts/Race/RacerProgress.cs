using System;

namespace Unbounded.Race
{
    /// <summary>
    /// One car's place in a Race, as the Standings see it: the Laps it has
    /// completed, the Checkpoints it has taken on the current Lap, and how far
    /// along the Track it is. Comparing two of these is how the field is
    /// ordered, so a car that cuts across the infield cannot jump a place by
    /// distance alone.
    /// </summary>
    public readonly struct RacerProgress : IComparable<RacerProgress>, IEquatable<RacerProgress>
    {
        public RacerProgress(int lapsCompleted, int checkpointsPassed, float distanceAlongTrack)
        {
            if (lapsCompleted < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lapsCompleted), lapsCompleted,
                    "A car cannot have completed a negative number of Laps.");
            }

            if (checkpointsPassed < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(checkpointsPassed), checkpointsPassed,
                    "A car cannot have passed a negative number of Checkpoints.");
            }

            LapsCompleted = lapsCompleted;
            CheckpointsPassed = checkpointsPassed;
            DistanceAlongTrack = distanceAlongTrack > 0f ? distanceAlongTrack : 0f;
        }

        /// <summary>Validated Laps taken this Race.</summary>
        public int LapsCompleted { get; }

        /// <summary>Checkpoints taken in order on the current Lap.</summary>
        public int CheckpointsPassed { get; }

        /// <summary>Metres along the current Lap from the start/finish line.</summary>
        public float DistanceAlongTrack { get; }

        /// <summary>
        /// Positive when this car is ahead of <paramref name="other"/>: more
        /// Laps, then more Checkpoints, then further along the Track.
        /// </summary>
        public int CompareTo(RacerProgress other)
        {
            int laps = LapsCompleted.CompareTo(other.LapsCompleted);
            if (laps != 0)
            {
                return laps;
            }

            int checkpoints = CheckpointsPassed.CompareTo(other.CheckpointsPassed);
            if (checkpoints != 0)
            {
                return checkpoints;
            }

            return DistanceAlongTrack.CompareTo(other.DistanceAlongTrack);
        }

        public bool Equals(RacerProgress other) =>
            LapsCompleted == other.LapsCompleted &&
            CheckpointsPassed == other.CheckpointsPassed &&
            DistanceAlongTrack.Equals(other.DistanceAlongTrack);

        public override bool Equals(object obj) => obj is RacerProgress other && Equals(other);

        public override int GetHashCode() =>
            ((LapsCompleted * 397) ^ CheckpointsPassed) * 397 ^ DistanceAlongTrack.GetHashCode();

        public override string ToString() =>
            LapsCompleted + " laps, " + CheckpointsPassed + " checkpoints, " +
            DistanceAlongTrack.ToString("0.0") + " m";
    }
}
