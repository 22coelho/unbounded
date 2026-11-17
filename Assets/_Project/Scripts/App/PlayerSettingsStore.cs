using Unbounded.Core;
using UnityEngine;

namespace Unbounded.App
{
    /// <summary>
    /// Reads and writes the small persistent slice of the game: the two
    /// settings and the player's best Lap. PlayerPrefs is deliberate here —
    /// there is no profile data worth inventing a file format for.
    /// </summary>
    public static class PlayerSettingsStore
    {
        private const string MasterVolumeKey = "settings.masterVolume";
        private const string SteeringSensitivityKey = "settings.steeringSensitivity";
        private const string BestLapKey = "race.bestLapTime";

        /// <summary>Loads the saved settings, or the defaults on a first run.</summary>
        public static GameSettings LoadSettings()
        {
            GameSettings defaults = GameSettings.Default;
            return new GameSettings(
                PlayerPrefs.GetFloat(MasterVolumeKey, defaults.MasterVolume),
                PlayerPrefs.GetFloat(SteeringSensitivityKey, defaults.SteeringSensitivity));
        }

        /// <summary>Writes both settings; call <see cref="Flush"/> when the player leaves pause.</summary>
        public static void SaveSettings(GameSettings settings)
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, settings.MasterVolume);
            PlayerPrefs.SetFloat(SteeringSensitivityKey, settings.SteeringSensitivity);
        }

        /// <summary>Writes the preferences to disk, so a quit keeps the latest values.</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        /// <summary>The saved best Lap in seconds, or zero when none is stored.</summary>
        public static float LoadBestLap() => PlayerPrefs.GetFloat(BestLapKey, 0f);

        public static void SaveBestLap(float seconds)
        {
            PlayerPrefs.SetFloat(BestLapKey, seconds);
            PlayerPrefs.Save();
        }

        /// <summary>Removes the stored best Lap, for a deliberate reset.</summary>
        public static void ClearBestLap()
        {
            PlayerPrefs.DeleteKey(BestLapKey);
            PlayerPrefs.Save();
        }
    }
}
