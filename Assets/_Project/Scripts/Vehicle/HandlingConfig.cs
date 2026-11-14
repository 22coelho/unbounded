using System;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// Tuning for the arcade handling model. Speeds are metres per second,
    /// forces are newtons, and angles are degrees unless stated otherwise.
    /// </summary>
    [Serializable]
    public struct HandlingConfig
    {
        public float MaxSpeed;
        public float MaxReverseSpeed;
        public float EngineForce;
        public float ReverseForce;
        public float BrakeForce;
        public float RollingResistance;
        public float DragCoefficient;

        public float MaxSteeringAngle;
        public float SteeringRate;
        public float WheelBase;
        public float FrontAxleDistance;
        public float RearAxleDistance;

        public float MaxLateralForce;
        public float LateralGripStiffness;
        public float HighSpeedGrip;
        public float BaseLateralAcceleration;
        public float GripEngageSpeed;

        public float YawResponse;
        public float MaxYawAcceleration;
        public float BrakeToReverseSpeed;

        public static HandlingConfig Default => new HandlingConfig
        {
            MaxSpeed = 60f,
            MaxReverseSpeed = 14f,
            EngineForce = 9000f,
            ReverseForce = 5000f,
            BrakeForce = 16000f,
            RollingResistance = 180f,
            DragCoefficient = 0.38f,

            MaxSteeringAngle = 32f,
            SteeringRate = 240f,
            WheelBase = 2.8f,
            FrontAxleDistance = 1.4f,
            RearAxleDistance = 1.4f,

            MaxLateralForce = 18000f,
            LateralGripStiffness = 20000f,
            HighSpeedGrip = 0.65f,
            BaseLateralAcceleration = 15f,
            GripEngageSpeed = 0.5f,

            YawResponse = 10f,
            MaxYawAcceleration = 30f,
            BrakeToReverseSpeed = 0.5f
        };
    }
}
