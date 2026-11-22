using System;
using System.Globalization;
using System.Linq;
using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class SkillLabHud
    {
        static readonly float[] Rates = { .25f, .5f, 1, 2 };
        static readonly string[] RateLabels = { "0.25×", "0.5×", "1×", "2×" };
        static readonly Color Ink = new Color32(232, 242, 248, 255);
        static readonly Color Muted = new Color32(168, 190, 202, 255);
        static readonly Color Cyan = new Color32(94, 234, 218, 255);
        static readonly Color PanelInk = new Color32(14, 22, 28, 250);
        static readonly Color Frame = new Color32(53, 73, 85, 255);
        static readonly Color Tile = new Color32(28, 42, 50, 255);
        static bool _debug, _showDebug;
        Vector2 _scroll;
        int _rate = 2;
        GUIStyle _heading, _title, _label, _caption, _number, _button, _cardName, _cardTooltip;

        public bool Ranges { get; private set; } = true;
        public bool Trajectories { get; private set; } = true;
        public bool DamageNumbers { get; private set; } = true;
        public float Rate => Rates[_rate];
        static float Scale => Mathf.Max(.45f, Mathf.Min(Screen.width / 1160f, Screen.height / 720f));
        static float Width => Screen.width / Scale;
        static float Height => Screen.height / Scale;
        static Rect Header => new Rect(20, 20, 280, 97);
        static Rect Stats => new Rect(Width - 250, 20, 230, 216);
        static Rect Dock => new Rect((Width - 804) * .5f, Height - 134, 804, 114);
        static Rect DebugPanel => new Rect(20, 127, 322, Mathf.Min(400, Height - 270));
        static Rect Pixels(Rect rect) => new Rect(rect.position * Scale, rect.size * Scale);
        public static Rect Bounds => Pixels(Stats);

        public static bool Covers(Vector2 guiPoint) => Pixels(Header).Contains(guiPoint) ||
            Bounds.Contains(guiPoint) || Pixels(Dock).Contains(guiPoint) ||
            (_debug && Pixels(DebugPanel).Contains(guiPoint));

        internal static bool OverlapsPanel(Rect bounds) => bounds.Overlaps(Pixels(Header)) ||
            bounds.Overlaps(Bounds) || bounds.Overlaps(Pixels(Dock)) ||
            (_debug && bounds.Overlaps(Pixels(DebugPanel)));

        public static bool PointerOver => Covers(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

        public void Draw(SkillLabRunner runner, string status)
        {
            if (Event.current.type == EventType.Layout) _debug = _showDebug;
            EnsureStyles();
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1)) * matrix;
            try
            {
                bool drawDebug = _debug;
                DrawHeader(runner, status);
                DrawStats(runner);
                DrawDock(runner, runner.Snapshot);
                if (drawDebug) DrawDebug(runner, runner.Snapshot);
            }
            finally { GUI.matrix = matrix; }
        }

        void DrawHeader(SkillLabRunner runner, string status)
        {
            var panel = Header;
            DrawPanel(panel);
            bool ready = runner.Snapshot != null;
            var error = runner.LastLoadError;
            string state = runner.ReplayMode ? (runner.ReplayExitPending ? "正在退出回放" : "回放")
                : error != null ? (ready ? "载入失败 · 保留当前场次" : "载入失败")
                : runner.Paused ? "已暂停" : runner.UsingDefaultSkills ? "默认演练 · 可试放" : ready ? "已发布 · 可试放" : "正在载入";
            Fill(new Rect(panel.x + 16, panel.y + 13, 4, 36), Cyan);
            GUI.Label(new Rect(panel.x + 29, panel.y + 9, 226, 30), "技能训练场", _title);
            GUI.Label(new Rect(panel.x + 29, panel.y + 40, 239, 22), new GUIContent(state, error ?? status), _caption);
            GUI.Label(new Rect(panel.x + 16, panel.y + 68, 250, 18),
                error != null ? "展开调试查看载入错误" : "WASD 移动   ·   滚轮缩放   ·   Esc 暂停", _caption);
        }

        void DrawStats(SkillLabRunner runner)
        {
            var panel = Stats;
            DrawPanel(panel);
            var state = runner.Snapshot;
            var performance = runner.GetComponent<SkillLabVisuals>();
            float x = panel.x + 16, y = panel.y;
            GUI.Label(new Rect(x, y + 10, 200, 23), "伤害 / 秒 · 最近 5 秒", _heading);
            string dps = performance == null || state == null || performance.MetricsGeneration != runner.Generation
                ? "—" : $"{(performance.DamageWindowIncomplete ? "≥ " : "")}{performance.DamagePerSecond:N1}";
            GUI.Label(new Rect(x, y + 33, 200, 42), dps, _number);
            Fill(new Rect(x, y + 82, 198, 1), Frame);
            Metric(x, y + 91, "总伤害", state == null ? "—" : state.Statistics.Damage.ToString("N1"));
            Metric(x, y + 116, "有效命中", state == null ? "—" : state.Statistics.Hits.ToString("N0"));
            string resetLabel = runner.ReplayExitPending ? "正在退出" : runner.ReplayMode ? "退出回放" : "重置场次";
            using (new EnabledScope(!runner.ReplayExitPending))
            {
                if (DrawButton(new Rect(x, y + 151, 96, 24), resetLabel))
                {
                    if (runner.ReplayMode) runner.ExitReplay();
                    else runner.ReloadPublished();
                }
            }
            if (DrawButton(new Rect(x + 102, y + 151, 96, 24), _debug ? "收起调试" : "展开调试")) _showDebug = !_debug;
            if (performance != null && DrawButton(new Rect(x, y + 181, 96, 24), performance.Muted ? "开启音效" : "静音"))
                performance.Muted = !performance.Muted;
            if (DrawButton(new Rect(x + 102, y + 181, 96, 24), "重置镜头")) performance?.ResetView();
        }

        void Metric(float x, float y, string name, string value)
        {
            GUI.Label(new Rect(x, y, 71, 22), name, _caption);
            _label.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + 68, y, 130, 22), value, _label);
            _label.alignment = TextAnchor.MiddleLeft;
        }

        void DrawDock(SkillLabRunner runner, SkillSimulationSnapshot state)
        {
            var dock = Dock;
            DrawPanel(dock);
            GUI.Label(new Rect(dock.x + 14, dock.y + 7, 122, 23), "试放技能", _heading);
            if (state?.Charging == true)
            {
                GUI.Label(new Rect(dock.x + 126, dock.y + 7, 280, 23), $"蓄力 {state.ChargeProgress:P0} · {state.CastPower:0.00}×", _caption);
                Fill(new Rect(dock.x + 412, dock.y + 15, 376, 4), Frame);
                Fill(new Rect(dock.x + 412, dock.y + 15, 376 * Mathf.Clamp01(state.ChargeProgress), 4), Cyan);
            }
            GUIContent hoveredContent = null;
            Rect hoveredCard = default;
            for (int i = 0; i < CombatControls.MainSkillCount; i++)
            {
                string slot = PublishedSkillSlots.Ordered[i];
                var control = CombatControls.At(i);
                var card = new Rect(dock.x + 12 + i * 196, dock.y + 34, 192, 67);
                bool occupied = runner.SlotSkills.TryGetValue(slot, out var skill);
                bool selected = runner.CurrentSlot == slot;
                string name = occupied ? runner.DisplaySkillName(skill) : "空槽位";
                string revision = runner.SlotRevisions.TryGetValue(slot, out var hash) ? hash : null;
                var content = new GUIContent("", $"{name}\n{control.Label} / {control.AlternateLabel}" +
                    (string.IsNullOrEmpty(revision) ? "" : $"\n{revision.Substring(0, Mathf.Min(8, revision.Length))}"));
                Fill(card, selected ? Cyan : Frame);
                Fill(new Rect(card.x + 1, card.y + 1, card.width - 2, card.height - 2), selected ? new Color32(24, 56, 60, 255) : Tile);
                using (new EnabledScope(occupied && !runner.ReplayMode))
                    if (GUI.Button(card, content, GUIStyle.none)) runner.SelectSlot(slot);
                Fill(new Rect(card.x + 9, card.y + 9, 44, 23), selected ? new Color32(41, 83, 85, 255) : Frame);
                GUI.Label(new Rect(card.x + 16, card.y + 9, 36, 23), control.Label, _heading);
                GUI.Label(new Rect(card.x + 61, card.y + 8, 90, 25), control.AlternateLabel, _caption);
                if (occupied && state != null && state.Cooldowns.TryGetValue(skill, out var cooldown) && cooldown > 0)
                {
                    _caption.alignment = TextAnchor.MiddleRight;
                    GUI.Label(new Rect(card.x + 134, card.y + 8, 49, 25), $"{cooldown:0.0}s", _caption);
                    _caption.alignment = TextAnchor.MiddleLeft;
                }
                var nameRect = new Rect(card.x + 9, card.y + 35, card.width - 18, 26);
                if (Event.current.type == EventType.Repaint)
                {
                    GUI.Label(nameRect, Ellipsize(name, _cardName, nameRect.width), _cardName);
                    if (occupied && card.Contains(Event.current.mousePosition))
                    {
                        hoveredContent = new GUIContent(content.tooltip);
                        hoveredCard = card;
                    }
                }
            }
            if (hoveredContent != null) DrawCardTooltip(hoveredCard, hoveredContent);
        }

        void DrawCardTooltip(Rect card, GUIContent content)
        {
            const float padding = 10;
            float width = Mathf.Clamp(_cardTooltip.CalcSize(content).x + 2 * padding, card.width, 460);
            float height = _cardTooltip.CalcHeight(content, width - 2 * padding) + 2 * padding;
            float x = Mathf.Clamp(card.x, 20, Width - width - 20);
            var bounds = new Rect(x, Dock.y - height - 8, width, height);
            DrawPanel(bounds);
            GUI.Label(new Rect(x + padding, bounds.y + padding, width - 2 * padding, height - 2 * padding), content, _cardTooltip);
        }

        static string Ellipsize(string text, GUIStyle style, float width)
        {
            text = text.Replace('\r', ' ').Replace('\n', ' ');
            var content = new GUIContent(text);
            if (style.CalcSize(content).x <= width) return text;

            var characterStarts = StringInfo.ParseCombiningCharacters(text);
            int low = 0, high = characterStarts.Length - 1;
            while (low < high)
            {
                int next = (low + high + 1) / 2;
                content.text = text.Substring(0, characterStarts[next]).TrimEnd() + "…";
                if (style.CalcSize(content).x <= width) low = next;
                else high = next - 1;
            }
            return characterStarts.Length == 0 ? text : text.Substring(0, characterStarts[low]).TrimEnd() + "…";
        }

        void DrawDebug(SkillLabRunner runner, SkillSimulationSnapshot state)
        {
            DrawPanel(DebugPanel);
            GUILayout.BeginArea(new Rect(DebugPanel.x + 12, DebugPanel.y + 6, DebugPanel.width - 24, DebugPanel.height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("调试 / 事件", _heading);
            GUILayout.BeginHorizontal();
            using (new EnabledScope(!runner.ReplayMode))
            {
                if (GUILayout.Button(runner.Paused ? "继续" : "暂停", _button)) runner.TogglePause();
                using (new EnabledScope(runner.Paused))
                    if (GUILayout.Button("单步 (.)", _button)) runner.AdvanceSimulation(1);
            }
            GUILayout.EndHorizontal();
            _rate = GUILayout.Toolbar(_rate, RateLabels);
            GUILayout.BeginHorizontal();
            Ranges = GUILayout.Toggle(Ranges, "范围");
            Trajectories = GUILayout.Toggle(Trajectories, "轨迹");
            DamageNumbers = GUILayout.Toggle(DamageNumbers, "跳字");
            GUILayout.EndHorizontal();
            if (runner.PendingPublication) GUILayout.Label("退出回放后将使用最新发布。", _caption);
            if (runner.HasDifferentSlotLayouts) GUILayout.Label("布阵取编号最小的已发布槽。", _caption);
            if (runner.LastLoadError != null) GUILayout.Label(runner.LastLoadError, WrapStyle(_caption));
            if (state != null)
            {
                GUILayout.Label($"{state.Time:0.00}s · 能量 {state.Energy:0.#} · 弹药 {state.Ammo}/{state.AmmoCapacity}", _caption);
                foreach (var entry in state.Events.Reverse().Where(IsDebugEvent).Take(6))
                {
                    var detail = entry.Damage != null ? $"{entry.PeriodicBuff ?? entry.Kind} {SkillLabFeedback.DamageAmount(entry):0.#} → {entry.Damage.Target}" : entry.Result;
                    GUILayout.Label($"{entry.Time:0.00}  {detail}", _caption);
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        static bool IsDebugEvent(SkillEventSnapshot e) => SkillLabFeedback.IsDamage(e) || e.Kind == "cast" || e.Kind == "rejected" || e.Kind.StartsWith("buff-", StringComparison.Ordinal);
        static GUIStyle WrapStyle(GUIStyle source) => new GUIStyle(source) { wordWrap = true, clipping = TextClipping.Overflow };

        static void DrawPanel(Rect panel)
        {
            Fill(new Rect(panel.x + 3, panel.y + 4, panel.width, panel.height), new Color32(0, 0, 0, 60));
            Fill(panel, Frame);
            Fill(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), PanelInk);
        }

        internal static void Fill(Rect bounds, Color color)
        {
            var original = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = original;
        }

        bool DrawButton(Rect rect, string text)
        {
            Fill(rect, Frame);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), rect.Contains(Event.current.mousePosition) ? Frame : Tile);
            return GUI.Button(rect, text, _button);
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = Text(22, Ink, true);
            _heading = Text(15, Ink, true);
            _label = Text(16, Ink, true);
            _caption = Text(14, Muted);
            _cardName = Text(17, Ink, true);
            _cardName.wordWrap = false;
            _cardTooltip = Text(14, Ink);
            _cardTooltip.wordWrap = true;
            _number = Text(34, Cyan, true);
            _button = Text(14, Ink, true);
            _button.alignment = TextAnchor.MiddleCenter;
            _button.hover.textColor = Cyan;
            _button.active.textColor = Cyan;
        }

        internal static GUIStyle Text(int size, Color color, bool bold = false)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(), richText = false
            };
            style.normal.textColor = color;
            return style;
        }

        readonly struct EnabledScope : IDisposable
        {
            readonly bool _previous;
            public EnabledScope(bool enabled) { _previous = GUI.enabled; GUI.enabled &= enabled; }
            public void Dispose() => GUI.enabled = _previous;
        }
    }
}
