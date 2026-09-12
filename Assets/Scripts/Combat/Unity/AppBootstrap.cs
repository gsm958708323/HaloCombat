using Combat.Game;
using UnityEngine;

namespace Combat.Unity.Game
{
    public sealed class AppBootstrap : MonoBehaviour
    {
        public GameObject TitleRoot,
            ResultRoot;
        public TitleView Title;
        public ResultView Result;
        public ArenaBootstrap Arena;
        public bool AutoStartIfNoTitle = true;
        GameFlow _flow;

        public GameFlow Flow => _flow;

        void Awake()
        {
            if (Arena != null)
                Arena.AutoStart = false;
        }

        void Start()
        {
            if (Arena == null)
            {
                Debug.LogError("AppBootstrap missing ArenaBootstrap");
                enabled = false;
                return;
            }

            _flow = Arena.CreateFlow(new PlayerPrefsSettingsStore());
            if (_flow == null)
            {
                enabled = false;
                return;
            }

            Title?.Bind(_flow);
            Result?.Bind(_flow);
            if (TitleRoot != null)
                TitleRoot.SetActive(true);
            if (ResultRoot != null)
                ResultRoot.SetActive(false);
            if (AutoStartIfNoTitle && Title == null && TitleRoot == null)
                _flow.StartRun();
        }

        void Update()
        {
            if (_flow == null)
                return;
            if (_flow.State == FlowState.Arena && TitleRoot != null)
                TitleRoot.SetActive(false);
            if (_flow.State == FlowState.Arena && ResultRoot != null)
                ResultRoot.SetActive(false);
            if (_flow.State == FlowState.Title && TitleRoot != null)
                TitleRoot.SetActive(true);
            if (_flow.State == FlowState.Title && ResultRoot != null)
                ResultRoot.SetActive(false);
            if (_flow.ResultReady && ResultRoot != null)
            {
                ResultRoot.SetActive(true);
                Result?.Show(_flow.Session != null && _flow.Session.PlayerWin);
            }
        }
    }
}
