using System;

namespace Unbounded.Track
{
    /// <summary>
    /// The ordered Checkpoints of a Lap, at fixed fractions of the Track length.
    /// Checkpoint zero is the first gate after the start/finish line; the line
    /// itself closes the Lap and is not a Checkpoint.
    /// </summary>
    public sealed class CheckpointRoute
    {
        public const int DefaultCount = 4;

        private readonly float[] _distances;

        public CheckpointRoute(float lapLength, int count = DefaultCount)
        {
            if (lapLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lapLength), lapLength,
                    "A Lap needs a positive length.");
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count,
                    "A Lap needs at least one Checkpoint.");
            }

            LapLength = lapLength;
            _distances = new float[count];

            // Count + 1 gaps, so no Checkpoint sits on the start/finish line.
            for (int i = 0; i < count; i++)
            {
                _distances[i] = lapLength * (i + 1f) / (count + 1f);
            }
        }

        public float LapLength { get; }

        public int Count => _distances.Length;

        public float DistanceAt(int index)
        {
            if (index < 0 || index >= _distances.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "There is no Checkpoint at that index.");
            }

            return _distances[index];
        }

        public float FractionAt(int index) => DistanceAt(index) / LapLength;

        /// <summary>Normalises a distance into [0, LapLength), wrapping at the seam.</summary>
        public float Wrap(float distance)
        {
            float wrapped = distance % LapLength;
            if (wrapped < 0f)
            {
                wrapped += LapLength;
            }

            return wrapped >= LapLength ? wrapped - LapLength : wrapped;
        }
    }
}
