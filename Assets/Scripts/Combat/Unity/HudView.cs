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

        public void Refresh(PresentHub hub, float dt)
        {
            if (hub == null || !hub.TryGetLocalHud(out var s))
            {
                if (HpSlider != null)
                    HpSlider.gameObject.SetActive(false);
                return;
            }
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
    }
}
