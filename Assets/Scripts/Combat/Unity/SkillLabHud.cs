using System.Linq;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class SkillLabHud
    {
        static readonly float[] Rates = { .25f, .5f, 1, 2 };
        static readonly string[] RateLabels = { "0.25×", "0.5×", "1×", "2×" };
        Vector2 _scroll;
        int _rate = 2;
        bool _showEvents = true;
        public bool Ranges { get; private set; } = true;
        public bool Trajectories { get; private set; } = true;
        public bool DamageNumbers { get; private set; } = true;
        public float Rate => Rates[_rate];
        static float Scale => Mathf.Clamp(Screen.height / 740f, .85f, 1.5f);
        static Rect Panel => new Rect(12, 12, Mathf.Clamp(Screen.width / Scale * .34f, 240, 340),
            Mathf.Min(410, Mathf.Max(80, Screen.height / Scale - 24)));
        public static Rect Bounds
        {
            get
            {
                var panel = Panel;
                return new Rect(panel.position * Scale, panel.size * Scale);
            }
        }
        public static bool PointerOver => Bounds.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));

        public void Draw(SkillLabRunner runner, string status)
        {
            var original = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1)) * original;
            GUILayout.BeginArea(Panel, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("HaloCombat  /  Skill Lab", GUI.skin.box);
            GUILayout.BeginHorizontal();
            foreach (string slot in new[] { "f6", "f7" })
            {
                bool selected = runner.CurrentSlot == slot;
                using (new EnabledScope(runner.SlotSkills.ContainsKey(slot) && !runner.ReplayMode))
                    if (GUILayout.Toggle(selected, slot.ToUpperInvariant(), GUI.skin.button) && !selected)
                        runner.SelectSlot(slot);
            }
            if (GUILayout.Button("Reload · F5")) runner.ReloadPublished();
            GUILayout.EndHorizontal();
            if (runner.PendingPublication) GUILayout.Label("New publication on disk");
            if (runner.HasDifferentSlotLayouts) GUILayout.Label("Using F6 layout. Sync both slots in the web editor.");
            GUILayout.Label(status);

            var state = runner.Snapshot;
            if (state != null)
            {
                foreach (var slot in runner.SlotSkills)
                    GUILayout.Label($"{slot.Key.ToUpperInvariant()}  {slot.Value}  ·  {runner.SlotRevisions[slot.Key].Substring(0, 8)}");
                var aim = runner.AimPreview;
                if (aim != null)
                {
                    float cooldown = state.Cooldowns.TryGetValue(runner.SkillId, out var remaining) ? remaining : 0;
                    GUILayout.Label($"{aim.Name}   ·   cooldown {cooldown:0.00}s");
                }
                GUILayout.BeginHorizontal();
                using (new EnabledScope(!runner.ReplayMode))
                {
                    if (GUILayout.Button(runner.Paused ? "Play" : "Pause")) runner.TogglePause();
                    using (new EnabledScope(runner.Paused))
                        if (GUILayout.Button("Step")) runner.StepLogicForTests(1);
                }
                if (GUILayout.Button("Reset view")) runner.GetComponent<SkillLabVisuals>()?.ResetView();
                GUILayout.EndHorizontal();
                _rate = GUILayout.Toolbar(_rate, RateLabels);
                GUILayout.Label($"{state.Time:0.00}s   ·   Energy {state.Energy:0.#}   ·   Ammo {state.Ammo}/{state.AmmoCapacity}");
                if (state.Charging) GUILayout.Label($"Charging {state.ChargeProgress:P0} / {state.CastPower:0.00}×");
                GUILayout.Label($"Casts {state.Statistics.Casts}   ·   Hits {state.Statistics.Hits}   ·   Damage {state.Statistics.Damage:0.#}");

                GUILayout.BeginHorizontal();
                Ranges = GUILayout.Toggle(Ranges, "Ranges");
                Trajectories = GUILayout.Toggle(Trajectories, "Trails");
                DamageNumbers = GUILayout.Toggle(DamageNumbers, "Numbers");
                GUILayout.EndHorizontal();
                _showEvents = GUILayout.Toggle(_showEvents, "Recent events");
                if (_showEvents)
                {
                    foreach (var entry in state.Events.Reverse().Where(e => e.Damage != null || e.Kind == "cast" || e.Kind == "rejected" || e.Kind.StartsWith("buff-")).Take(6))
                    {
                        string detail = entry.Damage != null ? $"{entry.PeriodicBuff ?? entry.Kind} {entry.Damage.HpLost:0.#} → {entry.Damage.Target}" : entry.Result;
                        GUILayout.Label($"{entry.Time:0.00}  {detail}");
                    }
                }
            }
            GUILayout.Label("WASD move   ·   Click or hold to cast   ·   Wheel zoom");
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = original;
        }

        readonly struct EnabledScope : System.IDisposable
        {
            readonly bool _previous;
            public EnabledScope(bool enabled) { _previous = GUI.enabled; GUI.enabled &= enabled; }
            public void Dispose() => GUI.enabled = _previous;
        }
    }
}
