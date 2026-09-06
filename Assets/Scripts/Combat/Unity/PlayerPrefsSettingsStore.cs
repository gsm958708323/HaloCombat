using Combat.Game;
using UnityEngine;

namespace Combat.Unity.Game
{
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        const string VolumeKey = "HaloCombat.MasterVolume";
        const string ShakeKey = "HaloCombat.ScreenShake";
        const string HitboxKey = "HaloCombat.ShowHitboxes";

        public GameSettings Load()
        {
            return new GameSettings
            {
                MasterVolume = PlayerPrefs.GetFloat(VolumeKey, 1f),
                ScreenShake = PlayerPrefs.GetInt(ShakeKey, 1) != 0,
                ShowHitboxes = PlayerPrefs.GetInt(HitboxKey, 0) != 0,
            };
        }

        public void Save(GameSettings settings)
        {
            if (settings == null)
                return;
            PlayerPrefs.SetFloat(VolumeKey, settings.MasterVolume);
            PlayerPrefs.SetInt(ShakeKey, settings.ScreenShake ? 1 : 0);
            PlayerPrefs.SetInt(HitboxKey, settings.ShowHitboxes ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
