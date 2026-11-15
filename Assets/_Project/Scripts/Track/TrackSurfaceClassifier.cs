using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// Turns a lateral offset into a Track surface. Kept separate from the
    /// generated geometry so gameplay can ask the question without a raycast.
    /// </summary>
    public static class TrackSurfaceClassifier
    {
        public static TrackSurface Classify(float lateralOffset, TrackShape shape)
        {
            float lateral = Mathf.Abs(lateralOffset);

            if (lateral <= shape.HalfWidth)
            {
                return TrackSurface.Road;
            }

            return lateral <= shape.EdgeOffset ? TrackSurface.Kerb : TrackSurface.Grass;
        }
    }
}
