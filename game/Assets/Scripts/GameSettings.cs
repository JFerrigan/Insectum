using UnityEngine;

namespace Insectum.Game
{
    // Player-facing audio levels, 0..10. Stored now so audio can read them once it exists.
    internal static class GameSettings
    {
        public const int MaxLevel = 10;
        private const string SfxKey = "settings.sfx";
        private const string MusicKey = "settings.music";

        public static int SfxLevel
        {
            get => PlayerPrefs.GetInt(SfxKey, 8);
            set { PlayerPrefs.SetInt(SfxKey, Mathf.Clamp(value, 0, MaxLevel)); PlayerPrefs.Save(); }
        }

        public static int MusicLevel
        {
            get => PlayerPrefs.GetInt(MusicKey, 8);
            set { PlayerPrefs.SetInt(MusicKey, Mathf.Clamp(value, 0, MaxLevel)); PlayerPrefs.Save(); }
        }

        public static float SfxVolume => SfxLevel / (float)MaxLevel;
        public static float MusicVolume => MusicLevel / (float)MaxLevel;
    }
}
