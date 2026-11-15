namespace Unbounded.Track
{
    /// <summary>The surface under a car, chosen by its distance from the Centreline.</summary>
    public enum TrackSurface
    {
        /// <summary>The road surface between the Kerbs.</summary>
        Road,

        /// <summary>The raised strip at the edge of the road.</summary>
        Kerb,

        /// <summary>Everything beyond the Kerbs.</summary>
        Grass
    }
}
