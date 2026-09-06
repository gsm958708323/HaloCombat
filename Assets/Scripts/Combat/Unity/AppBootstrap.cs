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
            Arena.SetExternalDriver(true);
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
            _flow.TickUpdate(Time.deltaTime);
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

        void LateUpdate()
        {
            Arena?.TickExternalLate(Time.deltaTime);
        }

        void OnGUI()
        {
            var old = GUI.color;
            GUI.color = new Color(.9f, .95f, 1f);
            if (_flow == null) { GUI.color = old; return; }
            if (_flow.State == FlowState.Title)
            {
                var box = new Rect(Screen.width * .5f - 220f, Screen.height * .5f - 175f, 440f, 350f);
                GUI.Box(box, "");
                GUI.Label(new Rect(box.x + 35, box.y + 28, 370, 58), "STICKMAN\nARENA", TitleStyle(34));
                GUI.Label(new Rect(box.x + 38, box.y + 105, 360, 30), "WASD Move  J Attack  K/L/I Skills  SHIFT Dodge", LabelStyle());
                GUI.Label(new Rect(box.x + 38, box.y + 137, 360, 24), "职业 / CLASS", LabelStyle());
                var classes = _flow.PlayerClasses;
                for (int i = 0; i < classes.Count; i++)
                {
                    float w = 360f / classes.Count;
                    var selected = string.Equals(classes[i], _flow.SelectedPlayerClassId, System.StringComparison.Ordinal);
                    var oldColor = GUI.color;
                    if (selected) GUI.color = new Color(.95f, .75f, .25f);
                    if (GUI.Button(new Rect(box.x + 38f + w * i, box.y + 160f, w - 6f, 34f), classes[i], ButtonStyle(14)))
                        _flow.SelectPlayerClass(classes[i]);
                    GUI.color = oldColor;
                }
                if (GUI.Button(new Rect(box.x + 90, box.y + 208, 260, 48), "START BATTLE", ButtonStyle(18)))
                    _flow.StartRun();
                GUI.Label(new Rect(box.x + 38, box.y + 278, 360, 26), "F3 hitboxes   ESC pause", LabelStyle());
            }
            else if (_flow.State == FlowState.Result && _flow.ResultReady)
            {
                var box = new Rect(Screen.width * .5f - 190f, Screen.height * .5f - 110f, 380f, 220f);
                GUI.Box(box, "");
                GUI.Label(new Rect(box.x + 70, box.y + 30, 250, 44), "BATTLE COMPLETE", TitleStyle(24));
                if (GUI.Button(new Rect(box.x + 50, box.y + 100, 130, 42), "RETRY", ButtonStyle(16))) _flow.ConfirmResultRetry();
                if (GUI.Button(new Rect(box.x + 200, box.y + 100, 130, 42), "TITLE", ButtonStyle(16))) _flow.ConfirmResultTitle();
            }
            else if (_flow.State == FlowState.Arena && _flow.Session != null && _flow.Session.Paused)
            {
                var box = new Rect(Screen.width * .5f - 150f, Screen.height * .5f - 85f, 300f, 170f);
                GUI.Box(box, ""); GUI.Label(new Rect(box.x + 35, box.y + 22, 230, 36), "PAUSED", TitleStyle(26));
                if (GUI.Button(new Rect(box.x + 55, box.y + 82, 190, 40), "CONTINUE", ButtonStyle(16))) _flow.Session.TogglePause();
            }
            GUI.color = old;
        }

        static GUIStyle TitleStyle(int size) { var s = new GUIStyle(GUI.skin.label); s.fontSize = size; s.fontStyle = FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter; s.normal.textColor = new Color(.95f, .75f, .25f); return s; }
        static GUIStyle LabelStyle() { var s = new GUIStyle(GUI.skin.label); s.alignment = TextAnchor.MiddleCenter; s.normal.textColor = Color.white; return s; }
        static GUIStyle ButtonStyle(int size) { var s = new GUIStyle(GUI.skin.button); s.fontSize = size; s.fontStyle = FontStyle.Bold; return s; }
    }
}
