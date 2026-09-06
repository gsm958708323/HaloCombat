using Combat.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class PauseMenuView : MonoBehaviour
    {
        public Button ContinueButton;
        public Button TitleButton;
        GameFlow _flow;

        public void Bind(GameFlow flow)
        {
            Unbind();
            _flow = flow;
            ContinueButton?.onClick.AddListener(Continue);
            TitleButton?.onClick.AddListener(flow.GoTitle);
        }

        void Continue()
        {
            if (_flow != null && _flow.Session != null && _flow.Session.Paused)
                _flow.Session.TogglePause();
        }

        void OnDestroy()
        {
            Unbind();
        }

        void Unbind()
        {
            if (_flow == null)
                return;
            ContinueButton?.onClick.RemoveListener(Continue);
            TitleButton?.onClick.RemoveListener(_flow.GoTitle);
            _flow = null;
        }
    }
}
