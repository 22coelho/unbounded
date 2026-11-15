using System;
using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// The cross-section of a Track: the road width, the Kerbs outside it, and
    /// the Barriers beyond the Kerbs. Every generated mesh reads these numbers
    /// so the road, Kerbs, and Barriers cannot drift apart.
    /// </summary>
    [Serializable]
    public struct TrackShape
    {
        public float HalfWidth;
        public float KerbWidth;
        public float KerbHeight;
        public float BarrierMargin;
        public float BarrierHeight;
        public float BarrierThickness;
        public float RoadHeight;
        public float MeshSpacing;

        /// <summary>The outer edge of the road plus its Kerb.</summary>
        public float EdgeOffset => HalfWidth + KerbWidth;

        /// <summary>The track-facing face of the Barrier.</summary>
        public float BarrierOffset => HalfWidth + KerbWidth + BarrierMargin;

        public static TrackShape Default => new TrackShape
        {
            HalfWidth = 6.5f,
            KerbWidth = 1.2f,
            KerbHeight = 0.09f,
            BarrierMargin = 3f,
            BarrierHeight = 1.05f,
            BarrierThickness = 0.4f,
            RoadHeight = 0.02f,
            MeshSpacing = 2.5f
        };
    }
}
