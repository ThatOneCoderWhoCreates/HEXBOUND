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
        private const string BeastMemoryKey = "hexbound.beastMemory";
        private const string BotDifficultyKey = "hexbound.botDifficulty";
        private const string MapKey = "hexbound.map";

        /// <summary>Arena for offline matches and for hosting online: 0 Lava Pit, 1 Celestial Sanctuary, 2 Sky Ruins.</summary>
        public static int Map
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(MapKey, 1), 0, 2); }
            set { PlayerPrefs.SetInt(MapKey, Mathf.Clamp(value, 0, 2)); }
        }

        /// <summary>Versus Bot difficulty: 0 easy, 1 normal, 2 hard.</summary>
        public static int BotDifficulty
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(BotDifficultyKey, 1), 0, 2); }
            set { PlayerPrefs.SetInt(BotDifficultyKey, Mathf.Clamp(value, 0, 2)); }
        }

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

        /// <summary>What the beast has learned about this player, and its level (text, see BeastMemory).</summary>
        public static string BeastMemory
        {
            get { return PlayerPrefs.GetString(BeastMemoryKey, string.Empty); }
            set
            {
                PlayerPrefs.SetString(BeastMemoryKey, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Local input delay in ticks for online matches. Higher means fewer rollbacks but more delay.</summary>
        public static int InputDelay
        {
            get { return PlayerPrefs.GetInt(InputDelayKey, 2); }
            set { PlayerPrefs.SetInt(InputDelayKey, Mathf.Clamp(value, 0, 6)); }
        }
    }
}
