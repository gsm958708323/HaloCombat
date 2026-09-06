using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/ComboTable")]
    public sealed class ComboTableAsset : ScriptableObject
    {
        public ComboEntryAsset[] Entries;

        public ComboTableSO Bake()
        {
            var source = Entries ?? Array.Empty<ComboEntryAsset>();
            var result = new ComboEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var entry = source[i] ?? new ComboEntryAsset();
                var preAssets = entry.PreSkillAssets;
                var preValues = entry.PreSkills ?? Array.Empty<int>();
                var pre = preAssets != null && preAssets.Length > 0
                    ? new SkillNodeId[preAssets.Length]
                    : new SkillNodeId[preValues.Length];
                for (int j = 0; j < pre.Length; j++)
                    pre[j] = preAssets != null && preAssets.Length > 0
                        ? RequireSkill(preAssets[j]).Id
                        : new SkillNodeId(preValues[j]);
                var toSkill = entry.Skill != null ? RequireSkill(entry.Skill) : null;
                result[i] = new ComboEntry
                {
                    PreSkills = pre,
                    Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags ?? Array.Empty<int>(),
                    Priority = entry.Priority,
                    ToSkill = toSkill != null ? toSkill.Id : new SkillNodeId(entry.ToSkill),
                    Timeline = toSkill != null ? toSkill.Timeline : new TimelineId(entry.Timeline)
                };
            }
            return new ComboTableSO { Entries = result };
        }

        static SkillDefinition RequireSkill(SkillDefinitionAsset asset)
        {
            if (asset == null)
                throw new InvalidOperationException("Combo entry has an empty skill reference.");
            return asset.Bake();
        }
    }
}
