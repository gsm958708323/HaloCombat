using Combat.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class BuffArenaHudView : MonoBehaviour
    {
        public Text HpText;
        HudSnapshot _snapshot;

        public void Refresh(PresentHub hub, float dt)
        {
            if (hub == null || !hub.TryGetLocalHud(out var snapshot))
            {
                if (HpText != null) HpText.text = string.Empty;
                return;
            }
            _snapshot = snapshot;
            if (HpText == null) return;
            HpText.text = ((int)snapshot.Hp) + " / " + ((int)snapshot.MaxHp);
            HpText.color = snapshot.Hp01 > .3f ? Color.green : Color.red;
        }

        void OnGUI()
        {
            if (HpText != null || !_snapshot.Valid) return;
            GUI.color = _snapshot.Hp01 > .3f ? Color.green : Color.red;
            GUI.Label(new Rect(0f, 0f, 300f, 50f), ((int)_snapshot.Hp) + " / " + ((int)_snapshot.MaxHp));
            GUI.color = Color.white;
        }
    }
}
