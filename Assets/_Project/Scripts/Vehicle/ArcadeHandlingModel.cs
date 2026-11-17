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
        private float _nitroCharge = 1f;
        private float _nitroRechargeDelay;
        private bool _nitroLockedOut;

        public ArcadeHandlingModel(HandlingConfig config)
        {
            _config = config;
        }

        public HandlingConfig Config => _config;

        public float SteeringAngleDegrees => _steeringAngle;

        /// <summary>Nitro left in the tank, from 0 to 1.</summary>
        public float NitroCharge => _nitroCharge;

        /// <summary>Whether Nitro is being spent this step.</summary>
        public bool IsNitroActive { get; private set; }

        /// <summary>Whether the tank ran dry and must recharge before Nitro fires again.</summary>
        public bool IsNitroLockedOut => _nitroLockedOut;

        /// <summary>Fills the tank and clears any lockout, for a fresh Race or a car returned to the Grid.</summary>
        public void ResetNitro()
        {
            _nitroCharge = 1f;
            _nitroRechargeDelay = 0f;
            _nitroLockedOut = false;
            IsNitroActive = false;
        }

        public HandlingForces Step(VehicleMotion motion, HandlingInput input, float deltaTime) =>
            Step(motion, input, HandlingSurface.Road, deltaTime);

        public HandlingForces Step(
            VehicleMotion motion,
            HandlingInput input,
            HandlingSurface surface,
            float deltaTime)
        {
            float dt = MathF.Max(deltaTime, 0.0001f);
            float throttle = Clamp01(input.Throttle);
            float brake = Clamp01(input.Brake);
            float steer = Clamp(input.Steer, -1f, 1f);

            UpdateSteering(steer, dt);
            UpdateNitro(input.Nitro, dt);

            float forwardSpeed = motion.ForwardSpeed;
            float speed = MathF.Abs(forwardSpeed);
            float speed01 = Clamp01(speed / _config.MaxSpeed);
            float grip = Lerp(1f, _config.HighSpeedGrip, speed01) * surface.LateralGripMultiplier;

            float driveForce = ComputeDriveForce(forwardSpeed, throttle, brake, surface);
            if (IsNitroActive && forwardSpeed > -0.5f)
            {
                driveForce += ComputeNitroForce(speed);
            }

            return new HandlingForces
            {
                DriveForce = driveForce,
                LateralForce = ComputeLateralForce(motion.LateralSpeed, speed, grip),
                YawAcceleration = ComputeYawAcceleration(motion.YawRate, forwardSpeed, grip),
                SteeringAngleDegrees = _steeringAngle
            };
        }

        /// <summary>
        /// Drains the tank while Nitro is held and recharges it after a short
        /// delay once it is released. Running dry locks the Nitro out until it
        /// has built back up, so it cannot be held down forever.
        /// </summary>
        private void UpdateNitro(bool pressed, float dt)
        {
            bool canBoost = pressed && !_nitroLockedOut && _nitroCharge > 0f;

            if (canBoost)
            {
                _nitroCharge = MathF.Max(0f, _nitroCharge - dt / MathF.Max(0.01f, _config.NitroDuration));
                _nitroRechargeDelay = MathF.Max(0f, _config.NitroRechargeDelay);
                IsNitroActive = true;

                if (_nitroCharge <= 0f)
                {
                    _nitroCharge = 0f;
                    _nitroLockedOut = true;
                    IsNitroActive = false;
                }

                return;
            }

            IsNitroActive = false;

            if (_nitroRechargeDelay > 0f)
            {
                _nitroRechargeDelay = MathF.Max(0f, _nitroRechargeDelay - dt);
                return;
            }

            if (_nitroCharge < 1f)
            {
                _nitroCharge = MathF.Min(1f, _nitroCharge + dt / MathF.Max(0.01f, _config.NitroRechargeSeconds));
            }

            if (_nitroLockedOut && _nitroCharge >= _config.NitroUnlockCharge)
            {
                _nitroLockedOut = false;
            }
        }

        /// <summary>
        /// Extra push that tapers as the car nears the Nitro top speed, so the
        /// burst raises the ceiling instead of being swallowed by drag.
        /// </summary>
        private float ComputeNitroForce(float speed)
        {
            float authority = 1f - Clamp01(speed / MathF.Max(0.01f, _config.NitroTopSpeed));
            return _config.NitroForce * authority;
        }

        private void UpdateSteering(float steer, float dt)
        {
            float target = steer * _config.MaxSteeringAngle;
            _steeringAngle = MoveTowards(_steeringAngle, target, _config.SteeringRate * dt);
        }

        private float ComputeDriveForce(
            float forwardSpeed,
            float throttle,
            float brake,
            HandlingSurface surface)
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
                force += _config.EngineForce * throttle * engineAuthority * surface.EngineForceMultiplier;
            }

            if (MathF.Abs(forwardSpeed) > 0.05f)
            {
                float resistance = _config.RollingResistance * surface.RollingResistanceMultiplier +
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
