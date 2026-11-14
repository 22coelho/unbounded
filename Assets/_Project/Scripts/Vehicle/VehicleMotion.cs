namespace Unbounded.Vehicle
{
    /// <summary>
    /// The car's measured motion in its own frame of reference. The adapter
    /// fills this from the Rigidbody before stepping the handling model.
    /// </summary>
    public struct VehicleMotion
    {
        /// <summary>Metres per second along the car's forward axis.</summary>
        public float ForwardSpeed;

        /// <summary>Metres per second along the car's right axis.</summary>
        public float LateralSpeed;

        /// <summary>Radians per second around the car's up axis.</summary>
        public float YawRate;
    }
}
