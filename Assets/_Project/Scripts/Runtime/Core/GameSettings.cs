using UnityEngine;

namespace HEXBOUND.Core
{
    /// <summary>Player preferences saved between sessions.</summary>
    public static class GameSettings
    {
        private const string SensitivityKey = "hexbound.mouseSensitivity";
        private const string InvertYKey = "hexbound.invertY";
        private const string VolumeKey = "hexbound.volume";
        private const string ShowHitboxesKey = "hexbound.showHitboxes";
        private const string LastIpKey = "hexbound.lastIp";
        private const string InputDelayKey = "hexbound.inputDelay";

        public static float MouseSensitivity
        {
            get { return PlayerPrefs.GetFloat(SensitivityKey, 0.12f); }
            set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, 0.02f, 0.6f)); }
        }

        public static bool InvertY
        {
            get { return PlayerPrefs.GetInt(InvertYKey, 0) == 1; }
            set { PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0); }
        }

        public static float Volume
        {
            get { return PlayerPrefs.GetFloat(VolumeKey, 0.7f); }
            set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); }
        }

        public static bool ShowHitboxes
        {
            get { return PlayerPrefs.GetInt(ShowHitboxesKey, 0) == 1; }
            set { PlayerPrefs.SetInt(ShowHitboxesKey, value ? 1 : 0); }
        }

        public static string LastIp
        {
            get { return PlayerPrefs.GetString(LastIpKey, "127.0.0.1"); }
            set { PlayerPrefs.SetString(LastIpKey, value ?? string.Empty); }
        }

        /// <summary>Local input delay in ticks for online matches. Higher means fewer rollbacks but more delay.</summary>
        public static int InputDelay
        {
            get { return PlayerPrefs.GetInt(InputDelayKey, 2); }
            set { PlayerPrefs.SetInt(InputDelayKey, Mathf.Clamp(value, 0, 6)); }
        }
    }
}
