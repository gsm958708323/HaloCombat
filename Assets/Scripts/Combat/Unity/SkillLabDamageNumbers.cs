using System.Collections.Generic;
using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class SkillLabDamageNumbers
    {
        const float Duration = .86f;
        const int Capacity = 64;
        readonly List<Number> _numbers = new List<Number>();
        GUIStyle _normal, _critical, _periodic, _shadow;
        public int Count => _numbers.Count;
        public void Clear() => _numbers.Clear();

        public void Tick(bool visible, float now)
        {
            if (visible) _numbers.RemoveAll(number => now - number.Started >= Duration);
            else Clear();
        }

        public void Add(SkillEventSnapshot entry, float now)
        {
            if (_numbers.Count >= Capacity) _numbers.RemoveAt(0);
            var text = SkillLabFeedback.DamageAmount(entry).ToString("0.#");
            bool critical = entry.Kind == "critical";
            if (critical) text += "!";
            _numbers.Add(new Number
            {
                Origin = new Vector3(entry.TargetX.Value, 1.55f, entry.TargetZ.Value),
                Offset = (entry.Sequence % 5 - 2) * 7,
                Text = text,
                TargetId = entry.TargetId,
                Started = now,
                Critical = critical,
                Periodic = entry.PeriodicBuff != null
            });
        }

        public void Draw(Camera camera, Dictionary<string, Rect> actorLabels)
        {
            if (_normal == null)
            {
                _normal = SkillLabHud.Text(18, Color.white, true);
                _critical = SkillLabHud.Text(23, new Color(1, .79f, .37f), true);
                _periodic = SkillLabHud.Text(16, new Color(.67f, .89f, 1), true);
                _shadow = SkillLabHud.Text(18, new Color(.05f, .07f, .09f), true);
                _normal.alignment = _critical.alignment = _periodic.alignment = _shadow.alignment = TextAnchor.MiddleCenter;
            }
            var original = GUI.color;
            float scale = Mathf.Clamp(Screen.height / 720f, .8f, 1.5f);
            foreach (var number in _numbers)
            {
                float fraction = (Time.unscaledTime - number.Started) / Duration;
                var point = camera.WorldToScreenPoint(number.Origin);
                var screen = new Vector2(point.x + number.Offset * scale, Screen.height - point.y - (12 + fraction * 42) * scale);
                if (point.z <= 0) continue;
                var style = number.Critical ? _critical : number.Periodic ? _periodic : _normal;
                int baseSize = number.Critical ? 23 : number.Periodic ? 16 : 18;
                style.fontSize = _shadow.fontSize = Mathf.RoundToInt(baseSize * scale);
                var textSize = style.CalcSize(new GUIContent(number.Text));
                var size = new Vector2(textSize.x + 4 * scale, textSize.y + 4 * scale);
                if (!TryPlace(number.TargetId, screen, size, 6 * scale, actorLabels, out var bounds)) continue;
                GUI.color = new Color(original.r, original.g, original.b, original.a * Mathf.Clamp01((1 - fraction) * 3));
                GUI.Label(new Rect(bounds.x + 1, bounds.y + 1, bounds.width, bounds.height), number.Text, _shadow);
                GUI.Label(bounds, number.Text, style);
            }
            GUI.color = original;
        }

        static bool TryPlace(string targetId, Vector2 center, Vector2 size, float gap,
            Dictionary<string, Rect> actorLabels, out Rect bounds)
        {
            bounds = new Rect(center - size * .5f, size);
            var target = new Rect(center, Vector2.zero);
            if (targetId != null && actorLabels.TryGetValue(targetId, out var label) &&
                Mathf.Abs(center.x - label.center.x) <= label.width * .5f + 4 * gap)
                target = label;
            else if (IsClear(bounds, actorLabels))
                return true;

            // Keep the original rise and test the same side first throughout the effect.
            bounds.x = target.xMax + gap;
            if (IsClear(bounds, actorLabels)) return true;
            bounds.x = target.xMin - gap - size.x;
            if (IsClear(bounds, actorLabels)) return true;

            bounds.x = Mathf.Clamp(center.x - size.x * .5f, 2, Screen.width - size.x - 2);
            bounds.y = Mathf.Max(2, Mathf.Min(bounds.y, target.yMin - size.y - gap));
            return IsClear(bounds, actorLabels);
        }

        static bool IsClear(Rect bounds, Dictionary<string, Rect> actorLabels)
        {
            // Account for the one-pixel text shadow as well as the text itself.
            bounds.width += 1;
            bounds.height += 1;
            if (bounds.xMin < 1 || bounds.xMax > Screen.width - 1 || bounds.yMin < 1 ||
                bounds.yMax > Screen.height - 1 || SkillLabHud.OverlapsPanel(bounds)) return false;
            foreach (var label in actorLabels.Values)
                if (bounds.Overlaps(label)) return false;
            return true;
        }

        struct Number
        {
            public Vector3 Origin;
            public float Started, Offset;
            public string Text, TargetId;
            public bool Critical, Periodic;
        }
    }
}
