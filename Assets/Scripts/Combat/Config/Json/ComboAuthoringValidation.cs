using System;
using System.Collections.Generic;
using System.Linq;

namespace Combat.Config
{
    // Authoring metadata for combo layouts and cancel-bar grouping is validated independently from runtime resolution.
    internal static class ComboAuthoringValidation
    {
        internal static void Views(SkillProjectDocument project)
        {
            var editor = project.Editor;
            if (editor == null) return;
            var skills = new HashSet<string>(project.Skills.Select(s => s.Id));
            var rules = (project.Runtime?.Combos ?? Array.Empty<RuntimeComboDocument>()).ToDictionary(r => r.Id);

            if (editor.ComboCancelRuleIds != null)
            {
                if (editor.ComboCancelRuleIds.Length > 256)
                    Fail("$.editor.comboCancelRuleIds", "At most 256 cancel bar rule references are allowed.");
                var seen = new HashSet<string>();
                foreach (string id in editor.ComboCancelRuleIds)
                {
                    if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                        Fail("$.editor.comboCancelRuleIds", "Expected unique rule identities.");
                    if (!rules.TryGetValue(id, out var rule) || rule.PreSkills == null || rule.PreSkills.Length == 0)
                        Fail("$.editor.comboCancelRuleIds", "Cancel bar entries must reference existing transition rules with predecessors.");
                }
            }

            var views = editor.ComboViews;
            if (views == null) return;
            if (views.Length > 256) Fail("$.editor.comboViews", "At most 256 author views are allowed.");
            var ids = new HashSet<string>();
            foreach (var view in views)
            {
                string path = "$.editor.comboViews[" + view?.Id + "]";
                if (view == null || string.IsNullOrWhiteSpace(view.Id) || !ids.Add(view.Id) || string.IsNullOrWhiteSpace(view.Name) || view.Name.Length > 128)
                    Fail(path, "Expected a unique identity and a name up to 128 characters.");
                if (!skills.Contains(view.RootSkill ?? "")) Fail(path + ".rootSkill", "Unknown root skill.");
                if (view.RuleIds == null || view.RuleIds.Length > 1024 || view.RuleIds.Distinct().Count() != view.RuleIds.Length)
                    Fail(path + ".ruleIds", "Expected unique rule identities.");
                foreach (string id in view.RuleIds)
                    if (!rules.TryGetValue(id ?? "", out var rule) || rule.PreSkills.Length == 0)
                        Fail(path + ".ruleIds", "Views may reference existing action transitions only.");
                if (view.Positions == null || view.Positions.Length > 513) Fail(path + ".positions", "Invalid positions.");
                var positions = new HashSet<string>();
                foreach (var position in view.Positions)
                    if (position == null || !positions.Add(position.SkillId ?? "") || position.SkillId != "$start" && position.SkillId != "$idle" && !skills.Contains(position.SkillId ?? "") ||
                        !Finite(position.X) || !Finite(position.Y)) Fail(path + ".positions", "Expected unique existing skills and finite coordinates.");
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Fail(string path, string message) => throw new ContentValidationException(path, message);
    }
}
