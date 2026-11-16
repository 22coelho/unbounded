using Unbounded.Core;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// The path around the Track that minimises lap time, derived from the
    /// Centreline. Each sample is pulled towards the midpoint of its neighbours
    /// while staying inside the Road, which relaxes a centreline into a smooth
    /// line that cuts to the inside of corners.
    /// </summary>
    public sealed class RacingLine : IRacingLine
    {
        /// <summary>Spacing of the samples the line is solved on.</summary>
        public const float SampleSpacing = 2.5f;

        /// <summary>How much Road is left between the line and the edge.</summary>
        public const float EdgeMargin = 2.5f;

        private const int RelaxationIterations = 600;

        private readonly TrackPath _path;

        private RacingLine(TrackPath path)
        {
            _path = path;
        }

        public float Length => _path.Length;

        public Vector3 SamplePosition(float distance) => _path.SamplePosition(distance);

        public Vector3 SampleDirection(float distance) => _path.SampleDirection(distance);

        public float Project(Vector3 position) => _path.Project(position).Distance;

        /// <summary>
        /// Builds the line from a Centreline and the cross-section it must stay
        /// within. The result is a closed path, shorter than the Centreline by
        /// the amount the corners are cut.
        /// </summary>
        public static RacingLine FromCentreline(TrackPath centreline, TrackShape shape)
        {
            int count = Mathf.Max(16, Mathf.CeilToInt(centreline.Length / SampleSpacing));
            float step = centreline.Length / count;
            float maxOffset = Mathf.Max(0.5f, shape.HalfWidth - EdgeMargin);

            var centre = new Vector3[count];
            var right = new Vector3[count];
            var points = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                float distance = i * step;
                centre[i] = centreline.SamplePosition(distance);
                right[i] = centreline.SampleRight(distance);
                points[i] = centre[i];
            }

            // Gauss-Seidel relaxation: repeatedly move each sample towards the
            // straight line between its neighbours, but never past the corridor
            // the Road allows. Repeated, this converges on a shorter path.
            for (int iteration = 0; iteration < RelaxationIterations; iteration++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 previous = points[(i - 1 + count) % count];
                    Vector3 next = points[(i + 1) % count];
                    Vector3 midpoint = (previous + next) * 0.5f;
                    float lateral = Vector3.Dot(midpoint - centre[i], right[i]);
                    points[i] = centre[i] + right[i] * Mathf.Clamp(lateral, -maxOffset, maxOffset);
                }
            }

            return new RacingLine(TrackPath.FromPolyline(points));
        }
    }
}
