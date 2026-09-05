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
        GameFlow _flow;

        void Start()
        {
            _flow = new GameFlow(new UnityInputSource(null, null));
            Title?.Bind(_flow);
            Result?.Bind(_flow);
            if (TitleRoot != null)
                TitleRoot.SetActive(true);
            if (ResultRoot != null)
                ResultRoot.SetActive(false);
        }

        void Update()
        {
            if (_flow == null)
                return;
            _flow.TickUpdate(Time.deltaTime);
            if (_flow.State == FlowState.Arena && TitleRoot != null)
                TitleRoot.SetActive(false);
            if (_flow.ResultReady && ResultRoot != null)
            {
                ResultRoot.SetActive(true);
                Result?.Show(_flow.Session != null && _flow.Session.PlayerWin);
            }
        }

        void LateUpdate()
        {
            _flow?.TickLate(Time.deltaTime);
        }
    }
}
