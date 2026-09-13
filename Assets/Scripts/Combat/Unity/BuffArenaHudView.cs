using Combat.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class BuffArenaHudView : MonoBehaviour
    {
        public Text HpText;
        HudSnapshot _snapshot;
        string _message;
        float _messageTime;

        public void Refresh(PresentHub hub, float dt)
        {
            if (hub == null || !hub.TryGetLocalHud(out var snapshot))
            {
                if (HpText != null) HpText.text = string.Empty;
                return;
            }
            _snapshot = snapshot;
            _message = hub.LastGameplayMessage;
            _messageTime = hub.GameplayMessageTime;
            if (HpText == null) return;
            HpText.text = ((int)snapshot.Hp) + " / " + ((int)snapshot.MaxHp);
            HpText.color = snapshot.Hp01 > .3f ? Color.green : Color.red;
        }

        void OnGUI()
        {
            if (!_snapshot.Valid) return;
            if (HpText == null)
            {
                GUI.color = _snapshot.Hp01 > .3f ? Color.green : Color.red;
                GUI.Label(new Rect(0f, 0f, 300f, 50f), ((int)_snapshot.Hp) + " / " + ((int)_snapshot.MaxHp));
            }
            if (_messageTime > 0f && !string.IsNullOrEmpty(_message))
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(0f, 50f, 320f, 40f), _message);
            }
            GUI.color = Color.white;
        }
    }
}
