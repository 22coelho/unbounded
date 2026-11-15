using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Where a world position sits on the Track: how far around the Lap it is
    /// and how far to the right of the Centreline.
    /// </summary>
    public readonly struct TrackProjection
    {
        public TrackProjection(float distance, float lateralOffset, Vector3 closestPoint)
        {
            Distance = distance;
            LateralOffset = lateralOffset;
            ClosestPoint = closestPoint;
        }

        /// <summary>Metres from the start/finish seam, in [0, Length).</summary>
        public readonly float Distance;

        /// <summary>Metres to the right of the Centreline; negative is to the left.</summary>
        public readonly float LateralOffset;

        /// <summary>The nearest point on the Centreline.</summary>
        public readonly Vector3 ClosestPoint;
    }
}
