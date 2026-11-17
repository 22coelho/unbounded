using System;

namespace Unbounded.Core
{
    /// <summary>
    /// The player's preferences: how loud the game is and how sharply the
    /// steering input curves away from centre. Every value is clamped on the
    /// way in, so a corrupt save can never take the car out of range.
    /// </summary>
    public readonly struct GameSettings
    {
        public const float MinMasterVolume = 0f;
        public const float MaxMasterVolume = 1f;
        public const float DefaultMasterVolume = 0.8f;

        public const float MinSteeringSensitivity = 0.5f;
        public const float MaxSteeringSensitivity = 2f;
        public const float DefaultSteeringSensitivity = 1f;

        public GameSettings(float masterVolume, float steeringSensitivity)
        {
            MasterVolume = Clamp(masterVolume, MinMasterVolume, MaxMasterVolume);
            SteeringSensitivity = Clamp(
                steeringSensitivity, MinSteeringSensitivity, MaxSteeringSensitivity);
        }

        public static GameSettings Default =>
            new GameSettings(DefaultMasterVolume, DefaultSteeringSensitivity);

        /// <summary>Global playback level, from silent to full.</summary>
        public float MasterVolume { get; }

        /// <summary>How fast the steering curve rises away from centre.</summary>
        public float SteeringSensitivity { get; }

        public GameSettings WithMasterVolume(float masterVolume) =>
            new GameSettings(masterVolume, SteeringSensitivity);

        public GameSettings WithSteeringSensitivity(float steeringSensitivity) =>
            new GameSettings(MasterVolume, steeringSensitivity);

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
            {
                return min;
            }

            return value < min ? min : (value > max ? max : value);
        }
    }
}
