using System.Collections.Generic;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Combat.Unity
{
    [RequireComponent(typeof(CombatRunner))]
    public sealed class CombatLessonDirector : MonoBehaviour
    {
        CombatRunner _runner;
        Canvas _canvas;
        Text _title;
        Text _objective;
        Text _state;
        Text _events;
        Text _timeline;
        Text _controls;
        Font _font;
        CombatWorld _boundWorld;
        readonly Queue<string> _eventQueue = new Queue<string>(8);

        void Awake() { _runner = GetComponent<CombatRunner>(); BuildHud(); }
        void Start()
        {
            _runner.SetLessonAuto(true);
            BindWorld();
        }

        void OnDestroy()
        {
            UnbindWorld();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        void Update()
        {
            if (_runner == null || _runner.Lesson == null) return;
            BindWorld();
            if (Input.GetKeyDown(KeyCode.Space)) _runner.SetLessonAuto(!_runner.LessonAuto);
            if (Input.GetKeyDown(KeyCode.N)) { _runner.SetLessonAuto(false); _runner.StepLesson(); }
            if (Input.GetKeyDown(KeyCode.R)) _runner.ReplayLesson();
            var lesson = _runner.Lesson;
            var current = lesson.Steps[lesson.StepIndex];
            _title.text = SceneTitle(_runner.Scene) + "  /  ARPG COMBAT LESSON";
            _objective.text = "STEP " + (lesson.StepIndex + 1) + "  " + current.Title + "\n" + current.Explanation;
            _state.text = BuildState(lesson);
            _timeline.text = BuildTimeline(lesson);
            _controls.text = (_runner.LessonAuto ? "AUTO PLAYING" : "PAUSED") + "   SPEED " + _runner.LessonSpeed.ToString("0.00") +
                "\n[Space] Pause   [N] Step   [R] Replay   [F5] Debug";
            if (_runner.LessonAuto && lesson.Finished) _runner.SetLessonAuto(false);
        }

        void BindWorld()
        {
            if (_boundWorld == _runner.World) return;
            UnbindWorld();
            _boundWorld = _runner.World;
            if (_boundWorld == null) return;
            _boundWorld.Events.Subscribe<EvCue>(OnCue);
            _boundWorld.Events.Subscribe<EvDamage>(OnDamageEvent);
            _boundWorld.Events.Subscribe<EvHitstop>(OnHitstopEvent);
            _boundWorld.Events.Subscribe<EvEntitySpawn>(OnSpawnEvent);
            _boundWorld.Events.Subscribe<EvEntityCleanup>(OnCleanupEvent);
        }

        void UnbindWorld()
        {
            if (_boundWorld == null) return;
            _boundWorld.Events.Unsubscribe<EvCue>(OnCue);
            _boundWorld.Events.Unsubscribe<EvDamage>(OnDamageEvent);
            _boundWorld.Events.Unsubscribe<EvHitstop>(OnHitstopEvent);
            _boundWorld.Events.Unsubscribe<EvEntitySpawn>(OnSpawnEvent);
            _boundWorld.Events.Unsubscribe<EvEntityCleanup>(OnCleanupEvent);
            _boundWorld = null;
        }

        void OnCue(EvCue e) => PushEvent("CUE " + e.CueId + " / " + e.Name);
        void OnDamageEvent(EvDamage e) => PushEvent("DAMAGE " + e.Amount.ToString("F0") + " -> " + e.Target);
        void OnHitstopEvent(EvHitstop e) => PushEvent("HITSTOP " + e.LogicFrames + " logic frames");
        void OnSpawnEvent(EvEntitySpawn e) => PushEvent("SPAWN " + e.BlueprintId + " owner=" + e.Owner);
        void OnCleanupEvent(EvEntityCleanup e) => PushEvent("CLEANUP " + e.Id + " " + e.Reason);

        string BuildState(CombatLesson lesson)
        {
            string result = "FRAME " + lesson.Frame + " / " + lesson.DurationFrames + "\n";
            if (_runner.TryGetPlayer(out var player))
            {
                var tf = player.GetComp<TransformComp>();
                var tags = player.GetComp<TagComp>();
                var state = player.GetComp<StateMachineComp>();
                result += "PLAYER  " + state.Current + "\n";
                result += "HP      " + player.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("0") + "\n";
                result += "POS     " + tf.Position.X.ToString("0.00") + ", " + tf.Position.Y.ToString("0.00") + ", " + tf.Position.Z.ToString("0.00") + "\n";
                result += "TAGS    " + (tags.Has(CommonTags.Grounded) ? "GROUNDED " : "") +
                    (tags.Has(CommonTags.Airborne) ? "AIRBORNE " : "") +
                    (tags.Has(CommonTags.Cancel) ? "CANCEL " : "") +
                    (tags.Has(CommonTags.Invincible) ? "IFRAME" : "") + "\n";
                if (player.TryGetComp<SkillDirectorComp>(out var director))
                    result += "SKILL   " + (director.IsPlaying ? director.CurrentSkill.ToString() : "IDLE") + "\n";
            }
            if (lesson.Target.IsValid && lesson.World.TryGetActor(lesson.Target, out var target))
            {
                result += "\nTARGET  " + target.Id + "\n";
                if (target.TryGetComp<AttributeSet>(out var attr)) result += "HP      " + attr.GetBase(AttrId.Hp).ToString("0") + "\n";
                if (target.TryGetComp<BehaviorTreeComp>(out var bt))
                    result += "BT      target=" + bt.Board.Target + "\nLEASH   " + bt.Board.LeashRange.ToString("0.0");
            }
            return result;
        }

        string BuildTimeline(CombatLesson lesson)
        {
            string result = "LESSON FLOW   ";
            for (int i = 0; i < lesson.Steps.Length; i++)
                result += i == lesson.StepIndex ? "[ ACTIVE ] " : "[  " + (i + 1) + "  ] ";
            result += "\n";
            for (int i = 0; i < lesson.Steps.Length; i++)
                result += (i == lesson.StepIndex ? ">> " : "   ") + lesson.Steps[i].Title + "  ";
            return result;
        }

        void PushEvent(string value)
        {
            if (_eventQueue.Count >= 6) _eventQueue.Dequeue();
            _eventQueue.Enqueue(value);
            string text = "EVENT STREAM\n";
            foreach (var item in _eventQueue) text += "• " + item + "\n";
            _events.text = text;
        }

        void BuildHud()
        {
            var go = new GameObject("CombatLessonHUD");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            go.AddComponent<GraphicRaycaster>();
            _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
            _title = Label("Header", _canvas.transform, 28, 1010, 760, 60, 28, Color.white);
            _objective = Label("Objective", _canvas.transform, 44, 790, 570, 170, 20, new Color(0.72f, 0.9f, 1f));
            _state = Label("State", _canvas.transform, 1460, 620, 410, 360, 17, new Color(0.78f, 0.94f, 1f));
            _events = Label("Events", _canvas.transform, 44, 150, 570, 290, 16, new Color(0.86f, 0.92f, 1f));
            _timeline = Label("Timeline", _canvas.transform, 570, 38, 780, 100, 16, new Color(0.95f, 0.72f, 0.28f));
            _controls = Label("Controls", _canvas.transform, 1370, 42, 500, 90, 16, new Color(0.55f, 1f, 0.82f));
            Panel(_canvas.transform, 20, 1000, 1880, 66, new Color(0.015f, 0.035f, 0.09f, 0.94f));
            Panel(_canvas.transform, 30, 770, 600, 210, new Color(0.015f, 0.04f, 0.1f, 0.88f));
            Panel(_canvas.transform, 1440, 600, 430, 380, new Color(0.015f, 0.04f, 0.1f, 0.88f));
            Panel(_canvas.transform, 30, 130, 600, 310, new Color(0.015f, 0.04f, 0.1f, 0.78f));
            Panel(_canvas.transform, 540, 25, 820, 125, new Color(0.015f, 0.04f, 0.1f, 0.9f));
            Button("PAUSE / PLAY", _canvas.transform, 1370, 965, 175, 35, () => _runner.SetLessonAuto(!_runner.LessonAuto));
            Button("STEP", _canvas.transform, 1555, 965, 100, 35, () => { _runner.SetLessonAuto(false); _runner.StepLesson(); });
            Button("REPLAY", _canvas.transform, 1665, 965, 175, 35, _runner.ReplayLesson);
        }

        Text Label(string name, Transform parent, float x, float y, float width, float height, int size, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            var text = go.AddComponent<Text>(); text.font = _font; text.fontSize = size; text.color = color; text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        void Panel(Transform parent, float x, float y, float width, float height, Color color)
        {
            var go = new GameObject("Panel"); go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            var image = go.AddComponent<Image>(); image.color = color;
            go.transform.SetAsFirstSibling();
        }

        void Button(string title, Transform parent, float x, float y, float width, float height, UnityAction action)
        {
            var go = new GameObject(title); go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
            var image = go.AddComponent<Image>(); image.color = new Color(0.04f, 0.22f, 0.34f, 0.95f);
            var button = go.AddComponent<Button>(); button.onClick.AddListener(action);
            var label = Label(title + "Text", go.transform, 0, 0, width, height, 15, Color.white);
            label.alignment = TextAnchor.MiddleCenter;
        }

        static string SceneTitle(CombatRunner.SceneKind scene)
        {
            switch (scene)
            {
                case CombatRunner.SceneKind.V1: return "V1 MOTOR";
                case CombatRunner.SceneKind.V2: return "V2 MELEE";
                case CombatRunner.SceneKind.V3: return "V3 PROJECTILE / AOE";
                default: return "V4 AI / SUMMON";
            }
        }
    }
}


