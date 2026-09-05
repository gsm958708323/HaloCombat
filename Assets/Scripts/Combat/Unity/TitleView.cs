using Combat.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class TitleView : MonoBehaviour
    {
        public Button StartButton,
            QuitButton;
        public Slider VolumeSlider;
        public Toggle ShakeToggle;

        public void Bind(GameFlow f)
        {
            if (StartButton != null)
                StartButton.onClick.AddListener(f.StartRun);
            if (QuitButton != null)
                QuitButton.onClick.AddListener(Application.Quit);
            if (VolumeSlider != null)
            {
                VolumeSlider.value = f.Settings.MasterVolume;
                VolumeSlider.onValueChanged.AddListener(v =>
                {
                    f.Settings.MasterVolume = v;
                    f.SaveSettings();
                });
            }
            if (ShakeToggle != null)
            {
                ShakeToggle.isOn = f.Settings.ScreenShake;
                ShakeToggle.onValueChanged.AddListener(v =>
                {
                    f.Settings.ScreenShake = v;
                    f.SaveSettings();
                });
            }
        }
    }
}
