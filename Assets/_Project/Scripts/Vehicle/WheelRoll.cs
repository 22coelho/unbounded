using System;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// Converts a car's forward speed into wheel rotation. Kept pure and
    /// separate from the adapter so the rolling relationship is testable: one
    /// revolution covers the wheel's circumference, and the angle is wrapped
    /// each step so a long Race cannot drift into bad float precision.
    /// </summary>
    public static class WheelRoll
    {
        /// <summary>The smallest radius the model will use, so a bad import cannot divide by zero.</summary>
        public const float MinRadius = 0.05f;

        private const float RadiansToDegrees = 180f / MathF.PI;

        /// <summary>Degrees the wheel turns in this step, before wrapping.</summary>
        public static float DeltaDegrees(float forwardSpeed, float radius, float deltaTime)
        {
            float safeSpeed = IsFinite(forwardSpeed) ? forwardSpeed : 0f;
            float safeRadius = IsFinite(radius) ? MathF.Max(radius, MinRadius) : MinRadius;
            float safeDeltaTime = IsFinite(deltaTime) ? MathF.Max(deltaTime, 0f) : 0f;
            return safeSpeed / safeRadius * RadiansToDegrees * safeDeltaTime;
        }

        /// <summary>Advances and wraps a wheel angle into -180..180 degrees.</summary>
        public static float Advance(float angleDegrees, float forwardSpeed, float radius, float deltaTime)
        {
            return Normalize(angleDegrees + DeltaDegrees(forwardSpeed, radius, deltaTime));
        }

        /// <summary>Wraps an angle into -180..180 degrees, rejecting NaN and infinities.</summary>
        public static float Normalize(float angleDegrees)
        {
            if (!IsFinite(angleDegrees))
            {
                return 0f;
            }

            angleDegrees %= 360f;
            if (angleDegrees >= 180f)
            {
                angleDegrees -= 360f;
            }
            else if (angleDegrees < -180f)
            {
                angleDegrees += 360f;
            }

            return angleDegrees;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
