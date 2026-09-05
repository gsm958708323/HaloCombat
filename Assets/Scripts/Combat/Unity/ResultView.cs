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

        public void Bind(GameFlow f)
        {
            Retry?.onClick.AddListener(f.ConfirmResultRetry);
            Title?.onClick.AddListener(f.ConfirmResultTitle);
        }

        public void Show(bool win)
        {
            gameObject.SetActive(true);
            if (ResultText != null)
                ResultText.text = win ? "VICTORY" : "DEFEAT";
        }
    }
}
