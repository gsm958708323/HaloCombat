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
                var preAssets = entry.PreSkillAssets ?? Array.Empty<SkillDefinitionAsset>();
                var pre = new SkillNodeId[preAssets.Length];
                for (int j = 0; j < pre.Length; j++)
                    pre[j] = RequireSkill(preAssets[j]).Id;
                var toSkill = RequireSkill(entry.Skill);
                result[i] = new ComboEntry
                {
                    PreSkills = pre,
                    Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags ?? Array.Empty<int>(),
                    Priority = entry.Priority,
                    ToSkill = toSkill.Id,
                    Timeline = toSkill.Timeline
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
