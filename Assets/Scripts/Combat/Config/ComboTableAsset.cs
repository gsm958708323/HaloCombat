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
                var preValues = entry.PreSkills ?? Array.Empty<int>();
                var pre = new SkillNodeId[preValues.Length];
                for (int j = 0; j < preValues.Length; j++) pre[j] = new SkillNodeId(preValues[j]);
                result[i] = new ComboEntry
                {
                    PreSkills = pre,
                    Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags ?? Array.Empty<int>(),
                    Priority = entry.Priority,
                    ToSkill = new SkillNodeId(entry.ToSkill),
                    Timeline = new TimelineId(entry.Timeline)
                };
            }
            return new ComboTableSO { Entries = result };
        }
    }
}
