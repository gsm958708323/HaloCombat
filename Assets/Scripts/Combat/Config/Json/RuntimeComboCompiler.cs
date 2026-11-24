using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    internal static class RuntimeComboCompiler
    {
        public static ComboEntry[] Compile(RuntimeComboDocument[] rows, Func<string, string, SkillDefinition> skill, Func<string, string, DurationSpec> buff)
        {
            var keys = new HashSet<string>();
            var result = new List<ComboEntry>();
            foreach (var row in rows)
            {
                string path = "$.runtime.combos[" + row?.Id + "]";
                if (row == null || string.IsNullOrWhiteSpace(row.Id) || !keys.Add(row.Id))
                    throw new ContentValidationException(path, "Missing or duplicate identity.");
                if (string.IsNullOrWhiteSpace(row.Input) || row.Input.Length > 128 || row.PreSkills == null || row.Conditions == null)
                    throw new ContentValidationException(path, "Invalid input, conditions or predecessor array.");
                var target = skill(row.ToSkill, path + ".toSkill");
                var preSkills = row.PreSkills.Select(id => skill(id, path + ".preSkills").Id).ToArray();
                var conditions = CompileConditions(row.Conditions, path + ".conditions", buff);
                var window = CompileWindow(row.Window, path + ".window");
                var entry = new ComboEntry
                {
                    Key = row.Id,
                    Input = new InputToken(row.Input),
                    Priority = row.Priority,
                    Conditions = conditions,
                    Window = window,
                    PreSkills = preSkills,
                    ToSkill = target.Id,
                    Timeline = target.Timeline
                };
                foreach (var other in result)
                    if (other.Input == entry.Input && other.Priority == entry.Priority && Overlaps(other.PreSkills, entry.PreSkills))
                        throw new ContentValidationException(path, "Ambiguous Combo priority with " + other.Key + "; rules sharing input and predecessor must use distinct priorities.");
                result.Add(entry);
            }
            return result.ToArray();
        }

        static ComboCondition[] CompileConditions(ComboConditionDocument[] items, string path, Func<string, string, DurationSpec> buff)
        {
            if (items.Length > 32) throw new ContentValidationException(path, "At most 32 conditions per combo rule are allowed.");
            var compiled = new ComboCondition[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                string p = path + "[" + i + "]";
                if (item == null || string.IsNullOrWhiteSpace(item.Kind))
                    throw new ContentValidationException(p, "Condition kind is required.");
                switch (item.Kind)
                {
                    case "tag":
                        if (!item.Tag.HasValue || item.Tag.Value <= 0)
                            throw new ContentValidationException(p + ".tag", "Expected a positive Tag identity.");
                        compiled[i] = ComboCondition.HasTag(item.Tag.Value, item.Negate);
                        break;
                    case "buff":
                    {
                        var spec = buff(item.Buff, p + ".buff");
                        var op = ParseOp(item.Op, p + ".op", allowLte: false);
                        float rawValue = item.Value ?? 1f;
                        if (!SkillSceneValidation.Finite(rawValue) || rawValue < 0 || rawValue > 128 || Math.Abs(rawValue - Math.Round(rawValue)) > .0001f)
                            throw new ContentValidationException(p + ".value", "Expected an integer stack count in [0, 128].");
                        compiled[i] = ComboCondition.Buff(spec.BuffId, spec.Key, op, (int)Math.Round(rawValue), item.Negate);
                        break;
                    }
                    case "resource":
                    {
                        ComboResourceKind resource = item.Resource == "energy" ? ComboResourceKind.Energy
                            : item.Resource == "ammo" ? ComboResourceKind.Ammo
                            : throw new ContentValidationException(p + ".resource", "Expected energy or ammo.");
                        var op = ParseOp(item.Op, p + ".op", allowLte: true);
                        float rawValue = item.Value ?? 0f;
                        AuthoredTimelineCompiler.Number(rawValue, 0f, 10000000f, p + ".value");
                        compiled[i] = ComboCondition.CheckResource(resource, op, rawValue, item.Negate);
                        break;
                    }
                    default:
                        throw new ContentValidationException(p + ".kind", "Unknown condition kind: " + item.Kind);
                }
            }
            return compiled;
        }

        static ComboCompareOp ParseOp(string op, string path, bool allowLte)
        {
            switch (op ?? "gte")
            {
                case "gte": return ComboCompareOp.Gte;
                case "eq": return ComboCompareOp.Eq;
                case "lt": return ComboCompareOp.Lt;
                case "lte":
                    if (allowLte) return ComboCompareOp.Lte;
                    break;
            }
            throw new ContentValidationException(path, "Unsupported comparison operator: " + op);
        }

        static ComboWindowSpec CompileWindow(ComboWindowDocument window, string path)
        {
            if (window == null || string.IsNullOrEmpty(window.Mode) || window.Mode == "default")
                return new ComboWindowSpec { Mode = ComboWindowMode.Default };
            switch (window.Mode)
            {
                case "channel":
                    if (string.IsNullOrWhiteSpace(window.Channel) || window.Channel.Length > 64)
                        throw new ContentValidationException(path + ".channel", "Expected a nonempty window channel up to 64 characters.");
                    return new ComboWindowSpec { Mode = ComboWindowMode.Channel, Channel = window.Channel };
                case "range":
                    if (!window.Start.HasValue || !window.End.HasValue)
                        throw new ContentValidationException(path, "Custom window range requires start and end.");
                    AuthoredTimelineCompiler.Number(window.Start.Value, 0f, 120f, path + ".start");
                    AuthoredTimelineCompiler.Number(window.End.Value, window.Start.Value + .000001f, 120f, path + ".end");
                    return new ComboWindowSpec { Mode = ComboWindowMode.Range, Start = window.Start.Value, End = window.End.Value };
                case "anytime":
                    return new ComboWindowSpec { Mode = ComboWindowMode.Anytime };
                default:
                    throw new ContentValidationException(path + ".mode", "Unknown combo window mode: " + window.Mode);
            }
        }

        static bool Overlaps(SkillNodeId[] left, SkillNodeId[] right)
        {
            if (left.Length == 0 || right.Length == 0) return left.Length == right.Length;
            return left.Any(id => Array.IndexOf(right, id) >= 0);
        }
    }
}
