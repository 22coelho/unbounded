using System;

namespace Unbounded.Vehicle
{
    /// <summary>
    /// Shapes the raw stick into steering input. Sensitivity is the exponent of
    /// the curve rather than a change to the handling parameters: above one
    /// sharpens the response near centre, below one gentles it, and full lock
    /// is always full lock. The handling model never sees the setting, so the
    /// car's grip and yaw behaviour stay exactly as tuned.
    /// </summary>
    public static class SteeringCurve
    {
        public const float MinSensitivity = 0.5f;
        public const float MaxSensitivity = 2f;

        /// <summary>Maps a raw stick reading in -1..1 through the sensitivity curve.</summary>
        public static float Apply(float raw, float sensitivity)
        {
            float input = float.IsNaN(raw) ? 0f : Clamp(raw, -1f, 1f);
            float magnitude = MathF.Abs(input);
            if (magnitude <= 0f)
            {
                return 0f;
            }

            float exponent = 1f / Clamp(float.IsNaN(sensitivity) ? 1f : sensitivity,
                MinSensitivity, MaxSensitivity);
            return MathF.Sign(input) * MathF.Pow(magnitude, exponent);
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
    }
}
