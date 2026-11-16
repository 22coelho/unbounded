using UnityEngine;

namespace Unbounded.AI
{
    /// <summary>
    /// What one opponent can see of itself and the player at a fixed step. The
    /// scene adapter fills this from the Rigidbody and the Race, so the driver
    /// itself stays pure and testable without a scene.
    /// </summary>
    public struct AiSituation
    {
        /// <summary>World position of the car.</summary>
        public Vector3 Position;

        /// <summary>World direction the car is pointing.</summary>
        public Vector3 Forward;

        /// <summary>Metres per second along the car's forward axis.</summary>
        public float ForwardSpeed;

        /// <summary>How far the player is ahead of this car along the Track; negative when behind.</summary>
        public float PlayerProgressAhead;
    }
}
