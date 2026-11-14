using System;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// The pure handling model. It owns the engine, brake, grip, and yaw
    /// response curves; the MonoBehaviour adapter owns the Rigidbody and
    /// applies the forces this model produces on a fixed timestep.
    /// </summary>
    public sealed class ArcadeHandlingModel
    {
        private readonly HandlingConfig _config;
        private float _steeringAngle;

        public ArcadeHandlingModel(HandlingConfig config)
        {
            _config = config;
        }

        public HandlingConfig Config => _config;

        public float SteeringAngleDegrees => _steeringAngle;

        public HandlingForces Step(VehicleMotion motion, HandlingInput input, float deltaTime)
        {
            float dt = MathF.Max(deltaTime, 0.0001f);
            float throttle = Clamp01(input.Throttle);
            float brake = Clamp01(input.Brake);
            float steer = Clamp(input.Steer, -1f, 1f);

            UpdateSteering(steer, dt);

            float forwardSpeed = motion.ForwardSpeed;
            float speed = MathF.Abs(forwardSpeed);
            float speed01 = Clamp01(speed / _config.MaxSpeed);
            float grip = Lerp(1f, _config.HighSpeedGrip, speed01);

            return new HandlingForces
            {
                DriveForce = ComputeDriveForce(forwardSpeed, throttle, brake),
                LateralForce = ComputeLateralForce(motion.LateralSpeed, speed, grip),
                YawAcceleration = ComputeYawAcceleration(motion.YawRate, forwardSpeed, grip),
                SteeringAngleDegrees = _steeringAngle
            };
        }

        private void UpdateSteering(float steer, float dt)
        {
            float target = steer * _config.MaxSteeringAngle;
            _steeringAngle = MoveTowards(_steeringAngle, target, _config.SteeringRate * dt);
        }

        private float ComputeDriveForce(float forwardSpeed, float throttle, float brake)
        {
            float force = 0f;

            if (brake > 0.001f)
            {
                if (forwardSpeed > _config.BrakeToReverseSpeed)
                {
                    force -= _config.BrakeForce * brake;
                }
                else if (forwardSpeed > -_config.MaxReverseSpeed)
                {
                    float reverseAuthority = 1f - Clamp01(-forwardSpeed / _config.MaxReverseSpeed);
                    force -= _config.ReverseForce * brake * reverseAuthority;
                }
            }
            else if (throttle > 0.001f && forwardSpeed < _config.MaxSpeed)
            {
                float engineAuthority = 1f - Clamp01(forwardSpeed / _config.MaxSpeed);
                force += _config.EngineForce * throttle * engineAuthority;
            }

            if (MathF.Abs(forwardSpeed) > 0.05f)
            {
                float resistance = _config.RollingResistance +
                                   _config.DragCoefficient * forwardSpeed * forwardSpeed;
                force -= MathF.Sign(forwardSpeed) * resistance;
            }

            return force;
        }

        private float ComputeLateralForce(float lateralSpeed, float forwardSpeed, float grip)
        {
            if (forwardSpeed < _config.GripEngageSpeed)
            {
                return 0f;
            }

            float force = -lateralSpeed * _config.LateralGripStiffness * grip;
            float limit = _config.MaxLateralForce * grip;
            return Clamp(force, -limit, limit);
        }

        private float ComputeYawAcceleration(float yawRate, float forwardSpeed, float grip)
        {
            float speed = MathF.Abs(forwardSpeed);
            if (speed < _config.GripEngageSpeed)
            {
                return 0f;
            }

            float steerRadians = _steeringAngle * (MathF.PI / 180f);
            float desiredYawRate = forwardSpeed * MathF.Tan(steerRadians) / _config.WheelBase;

            float maxLateralAcceleration = _config.BaseLateralAcceleration * grip;
            float maxYawRate = maxLateralAcceleration / speed;
            desiredYawRate = Clamp(desiredYawRate, -maxYawRate, maxYawRate);

            float yawError = desiredYawRate - yawRate;
            return Clamp(yawError * _config.YawResponse, -_config.MaxYawAcceleration, _config.MaxYawAcceleration);
        }

        private static float Clamp01(float value) => Clamp(value, 0f, 1f);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            float delta = target - current;
            if (MathF.Abs(delta) <= maxDelta)
            {
                return target;
            }

            return current + MathF.Sign(delta) * maxDelta;
        }
    }
}
