using UnityEngine;

namespace Unbounded.Core
{
    /// <summary>
    /// The Racing line the AI drives. A contract rather than a shared type, so
    /// an AI driver can follow a line without depending on the assembly that
    /// derives it from the Centreline.
    /// </summary>
    public interface IRacingLine
    {
        /// <summary>Total arc length in metres.</summary>
        float Length { get; }

        /// <summary>The point at a distance along the line; distances wrap at the seam.</summary>
        Vector3 SamplePosition(float distance);

        /// <summary>The direction of travel at a distance along the line.</summary>
        Vector3 SampleDirection(float distance);

        /// <summary>The arc length of the closest point on the line to a world position.</summary>
        float Project(Vector3 position);
    }
}
