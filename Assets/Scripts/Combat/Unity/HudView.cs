using Combat.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class HudView : MonoBehaviour
    {
        public Slider HpSlider;
        public Text HpText,
            BurnText;
        public GameObject CancelLamp,
            DevRoot;
        public float BarLerp = .12f;
        float _fill;
        HudSnapshot _snapshot;

        public void Refresh(PresentHub hub, float dt)
        {
            if (hub == null || !hub.TryGetLocalHud(out var s))
            {
                if (HpSlider != null)
                    HpSlider.gameObject.SetActive(false);
                return;
            }
            _snapshot = s;
            if (HpSlider != null)
            {
                HpSlider.gameObject.SetActive(true);
                var k = BarLerp <= 1e-4f ? 1 : 1 - Mathf.Exp(-dt / BarLerp);
                _fill = Mathf.Lerp(_fill, s.Hp01, k);
                HpSlider.value = _fill;
            }
            if (HpText != null)
                HpText.text = ((int)s.Hp) + "/" + ((int)s.MaxHp);
            if (BurnText != null)
                BurnText.text = s.BurnStacks > 0 ? "Burn " + s.BurnStacks : "";
            if (CancelLamp != null)
                CancelLamp.SetActive(s.Cancel);
            if (DevRoot != null)
                DevRoot.SetActive(true);
        }

        void OnGUI()
        {
            if (_snapshot.Valid && HpSlider == null)
            {
                var width = Mathf.Min(360f, Screen.width * .34f);
                GUI.Box(new Rect(22, 20, width + 24, 74), "");
                GUI.Label(new Rect(35, 28, width, 22), "PLAYER  " + Mathf.CeilToInt(_snapshot.Hp) + " / " + Mathf.CeilToInt(_snapshot.MaxHp));
                GUI.color = new Color(.12f, .8f, .35f); GUI.DrawTexture(new Rect(35, 54, width * _snapshot.Hp01, 16), Texture2D.whiteTexture); GUI.color = Color.white;
                GUI.Label(new Rect(35, 76, width, 22), _snapshot.IFrame ? "DODGE / I-FRAME" : (_snapshot.Casting ? "CASTING" : (_snapshot.Cancel ? "CANCEL READY" : "J ATTACK")));
                if (_snapshot.BurnStacks > 0) GUI.Label(new Rect(35, 98, width, 22), "BURN x" + _snapshot.BurnStacks);
                if (_snapshot.Hitstop) GUI.Label(new Rect(Screen.width - 150, 25, 120, 25), "HITSTOP");
            }
        }
    }
}
