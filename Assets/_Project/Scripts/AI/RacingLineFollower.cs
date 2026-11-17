using Unbounded.Core;
using UnityEngine;

namespace Unbounded.AI
{
    /// <summary>
    /// A racing-line follower. It projects itself onto the line, aims at a
    /// lookahead point that grows with speed, and picks a target speed from the
    /// curvature just ahead. It produces the same normalised input a human
    /// does, so an opponent is driven by the shared handling model rather than
    /// a second physics path.
    /// </summary>
    public sealed class RacingLineFollower
    {
        /// <summary>Catch-up never asks an opponent to go faster than this fraction of its top speed.</summary>
        public const float CatchUpCeiling = 0.99f;

        private const float ThrottleDeadband = 0.4f;

        private readonly AiConfig _config;

        private float _stuckSeconds;
        private float _reverseRemaining;

        public RacingLineFollower(AiConfig config)
        {
            _config = config;
        }

        /// <summary>Forgets any stall, for a restart or a car placed back on the Grid.</summary>
        public void Reset()
        {
            _stuckSeconds = 0f;
            _reverseRemaining = 0f;
        }

        /// <summary>
        /// One fixed step of driving. Returns neutral input if there is no line,
        /// so a half-built scene cannot drive a car off the Track.
        /// </summary>
        public AiControls Step(AiSituation situation, IRacingLine line, float deltaTime)
        {
            if (line == null)
            {
                return AiControls.Neutral;
            }

            float dt = Mathf.Max(0f, deltaTime);
            float speed = Mathf.Max(0f, situation.ForwardSpeed);
            float pace = EffectivePace(situation.PlayerProgressAhead);

            UpdateStall(situation.ForwardSpeed, dt);

            float distance = line.Project(situation.Position);
            float lookahead = Mathf.Max(2f, _config.LookaheadBase + speed * _config.LookaheadPerSpeed);
            float steer = SteerToward(line.SamplePosition(distance + lookahead), situation);

            if (_reverseRemaining > 0f)
            {
                _reverseRemaining = Mathf.Max(0f, _reverseRemaining - dt);
                // Reversing swings the nose the other way, so turn the wheel
                // away from the line the forwards driver would aim at.
                return new AiControls { Brake = 1f, Steer = -steer };
            }

            float curvature = MaxCurvatureAhead(line, distance, speed * _config.BrakingSeconds);
            bool useNitro = WantsNitro(situation, line, distance);
            float target = TargetSpeed(pace, curvature, useNitro);
            float error = target - speed;

            if (error > ThrottleDeadband)
            {
                return new AiControls
                {
                    Steer = steer,
                    Throttle = Mathf.Clamp01(0.5f + error * 0.25f),
                    Nitro = useNitro
                };
            }

            if (error < -ThrottleDeadband)
            {
                return new AiControls
                {
                    Steer = steer,
                    Brake = Mathf.Clamp01(-error * 0.25f)
                };
            }

            return new AiControls { Steer = steer, Throttle = 0.25f, Nitro = useNitro };
        }

        /// <summary>
        /// Nitro is spent only when it will pay for itself: a straight ahead,
        /// enough speed to make the push worthwhile, and charge still in the
        /// tank. The shared handling model owns the drain and the lockout, so
        /// the opponent plays by the same rules as the player.
        /// </summary>
        private bool WantsNitro(AiSituation situation, IRacingLine line, float distance)
        {
            if (situation.NitroCharge < _config.NitroMinCharge ||
                situation.NitroLockedOut ||
                situation.ForwardSpeed < _config.NitroMinSpeed)
            {
                return false;
            }

            // Only the immediate road matters here. The braking scan is longer
            // and looks into the next corner, which would ban Nitro down a
            // straight that happens to end in one.
            float curvature = MaxCurvatureAhead(line, distance, _config.NitroStraightLookahead);
            return curvature <= _config.NitroStraightCurvature;
        }

        /// <summary>
        /// The fraction of top speed to drive at. Catch-up only ever raises the
        /// pace, and only while the player is well ahead: an opponent that
        /// leads is never slowed to keep the Race artificially close.
        /// </summary>
        public float EffectivePace(float playerProgressAhead)
        {
            float boost = 0f;
            if (_config.CatchUpRange > 0f && playerProgressAhead > _config.CatchUpGap)
            {
                float ramp = (playerProgressAhead - _config.CatchUpGap) / _config.CatchUpRange;
                boost = _config.CatchUpBoost * Mathf.Clamp01(ramp);
            }

            return Mathf.Min(_config.Pace + boost, CatchUpCeiling);
        }

        private float TargetSpeed(float pace, float curvature, bool useNitro)
        {
            float ceiling = useNitro ? _config.NitroTopSpeed : _config.TopSpeed;
            float topSpeed = Mathf.Max(_config.MinCornerSpeed, ceiling * pace);
            float cornerSpeed = CornerSpeed(curvature, pace);
            return Mathf.Clamp(Mathf.Min(topSpeed, cornerSpeed), _config.MinCornerSpeed, topSpeed);
        }

        private float CornerSpeed(float curvature, float pace)
        {
            float budget = _config.LateralAccelerationBudget * pace * pace;

            if (curvature <= 0.0001f)
            {
                return float.MaxValue;
            }

            return Mathf.Sqrt(budget / curvature);
        }

        private float MaxCurvatureAhead(IRacingLine line, float distance, float scanDistance)
        {
            float probe = Mathf.Max(1f, _config.CurvatureProbe);
            float scan = Mathf.Clamp(scanDistance, probe, probe * 16f);
            float worst = 0f;

            for (float ahead = 0f; ahead <= scan; ahead += probe)
            {
                worst = Mathf.Max(worst, Curvature(line, distance + ahead, probe));
            }

            return worst;
        }

        private static float Curvature(IRacingLine line, float distance, float probe)
        {
            Vector3 before = line.SampleDirection(distance);
            Vector3 after = line.SampleDirection(distance + probe);
            float angle = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(before, after), Vector3.up),
                Vector3.Dot(before, after));
            return Mathf.Abs(angle) / probe;
        }

        private float SteerToward(Vector3 target, AiSituation situation)
        {
            Vector3 forward = situation.Forward;
            forward.y = 0f;

            Vector3 toTarget = target - situation.Position;
            toTarget.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || toTarget.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            forward.Normalize();
            toTarget.Normalize();

            float angle = Mathf.Atan2(
                Vector3.Dot(Vector3.Cross(forward, toTarget), Vector3.up),
                Vector3.Dot(forward, toTarget)) * Mathf.Rad2Deg;

            return Mathf.Clamp(angle / Mathf.Max(1f, _config.FullLockAngle), -1f, 1f);
        }

        private void UpdateStall(float forwardSpeed, float deltaTime)
        {
            if (_reverseRemaining > 0f)
            {
                _stuckSeconds = 0f;
                return;
            }

            if (deltaTime <= 0f || Mathf.Abs(forwardSpeed) > _config.StuckSpeed)
            {
                _stuckSeconds = 0f;
                return;
            }

            _stuckSeconds += deltaTime;
            if (_stuckSeconds >= _config.StuckSeconds)
            {
                _stuckSeconds = 0f;
                _reverseRemaining = _config.ReverseSeconds;
            }
        }
    }
}
