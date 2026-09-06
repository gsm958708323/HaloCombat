using Combat.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class TitleView : MonoBehaviour
    {
        public Button StartButton,
            QuitButton;
        public Slider VolumeSlider;
        public Toggle ShakeToggle;
        public Toggle HitboxToggle;
        GameFlow _flow;
        UnityAction<float> _volumeChanged;
        UnityAction<bool> _shakeChanged;
        UnityAction<bool> _hitboxChanged;

        public void Bind(GameFlow f)
        {
            Unbind();
            _flow = f;
            if (StartButton != null)
                StartButton.onClick.AddListener(f.StartRun);
            if (QuitButton != null)
                QuitButton.onClick.AddListener(Application.Quit);
            if (VolumeSlider != null)
            {
                VolumeSlider.value = f.Settings.MasterVolume;
                _volumeChanged = v =>
                {
                    f.Settings.MasterVolume = v;
                    f.SaveSettings();
                };
                VolumeSlider.onValueChanged.AddListener(_volumeChanged);
            }
            if (ShakeToggle != null)
            {
                ShakeToggle.isOn = f.Settings.ScreenShake;
                _shakeChanged = v =>
                {
                    f.Settings.ScreenShake = v;
                    f.SaveSettings();
                };
                ShakeToggle.onValueChanged.AddListener(_shakeChanged);
            }
            if (HitboxToggle != null)
            {
                HitboxToggle.isOn = f.Settings.ShowHitboxes;
                _hitboxChanged = v =>
                {
                    f.Settings.ShowHitboxes = v;
                    f.SaveSettings();
                };
                HitboxToggle.onValueChanged.AddListener(_hitboxChanged);
            }
        }

        void OnDestroy()
        {
            Unbind();
        }

        void Unbind()
        {
            if (StartButton != null && _flow != null)
                StartButton.onClick.RemoveListener(_flow.StartRun);
            if (VolumeSlider != null && _volumeChanged != null)
                VolumeSlider.onValueChanged.RemoveListener(_volumeChanged);
            if (ShakeToggle != null && _shakeChanged != null)
                ShakeToggle.onValueChanged.RemoveListener(_shakeChanged);
            if (HitboxToggle != null && _hitboxChanged != null)
                HitboxToggle.onValueChanged.RemoveListener(_hitboxChanged);
            _flow = null;
            _volumeChanged = null;
            _shakeChanged = null;
            _hitboxChanged = null;
        }
    }
}
