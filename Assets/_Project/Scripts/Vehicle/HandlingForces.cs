namespace Unbounded.Vehicle
{
    /// <summary>
    /// Forces and angular acceleration produced by one fixed step of the
    /// handling model. The adapter applies these to the Rigidbody.
    /// </summary>
    public struct HandlingForces
    {
        /// <summary>Newtons along the car's forward axis.</summary>
        public float DriveForce;

        /// <summary>Newtons along the car's right axis.</summary>
        public float LateralForce;

        /// <summary>Radians per second squared around the car's up axis.</summary>
        public float YawAcceleration;

        /// <summary>Current steering angle in degrees, for wheel visuals.</summary>
        public float SteeringAngleDegrees;
    }
}
