using UnityEngine;

namespace Unbounded.Track
{
    /// <summary>
    /// The authored circuit. One ordered list of control points defines the
    /// whole Track: straights, sweepers, and a hairpin at the far end.
    /// </summary>
    public static class TrackLayout
    {
        /// <summary>
        /// Control points in metres, ordered along the direction of travel, with
        /// the start/finish seam at the origin on a northbound straight.
        /// </summary>
        public static Vector3[] Circuit() => new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 80f),
            new Vector3(0f, 0f, 160f),
            new Vector3(-25f, 0f, 220f),
            new Vector3(-100f, 0f, 250f),
            new Vector3(-180f, 0f, 240f),
            new Vector3(-245f, 0f, 200f),
            new Vector3(-275f, 0f, 135f),
            new Vector3(-270f, 0f, 60f),
            new Vector3(-260f, 0f, -40f),
            new Vector3(-245f, 0f, -130f),
            new Vector3(-215f, 0f, -190f),
            new Vector3(-175f, 0f, -200f),
            new Vector3(-145f, 0f, -170f),
            new Vector3(-145f, 0f, -135f),
            new Vector3(-132f, 0f, -112f),
            new Vector3(-105f, 0f, -108f),
            new Vector3(-65f, 0f, -115f),
            new Vector3(-32f, 0f, -95f),
            new Vector3(0f, 0f, -60f)
        };
    }
}
