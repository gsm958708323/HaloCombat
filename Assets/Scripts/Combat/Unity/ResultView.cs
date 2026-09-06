using Combat.Game;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.Unity.Game
{
    public sealed class ResultView : MonoBehaviour
    {
        public Text ResultText;
        public Button Retry,
            Title;
        GameFlow _flow;

        public void Bind(GameFlow f)
        {
            Unbind();
            _flow = f;
            Retry?.onClick.AddListener(f.ConfirmResultRetry);
            Title?.onClick.AddListener(f.ConfirmResultTitle);
        }

        void OnDestroy()
        {
            Unbind();
        }

        void Unbind()
        {
            if (_flow == null)
                return;
            Retry?.onClick.RemoveListener(_flow.ConfirmResultRetry);
            Title?.onClick.RemoveListener(_flow.ConfirmResultTitle);
            _flow = null;
        }

        public void Show(bool win)
        {
            gameObject.SetActive(true);
            if (ResultText != null)
                ResultText.text = win ? "VICTORY" : "DEFEAT";
        }
    }
}
