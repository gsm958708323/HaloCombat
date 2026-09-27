using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [Serializable]
    public sealed class ComboEntryAsset
    {
        public BuffArenaSkillAsset[] PreSkillAssets;
        public string InputAction = "Fire1";
        public int[] RequiredTags;
        public int Priority;
        public BuffArenaSkillAsset Skill;
    }

    [CreateAssetMenu(menuName = "Combat/Buff Arena Combo Table")]
    public sealed class ComboTableAsset : ScriptableObject
    {
        public ComboEntryAsset[] Entries;
        public ComboTableSO Bake(BuffArenaSkillAsset[] skills)
        {
            var explicitTable = Bake();
            var combined = new System.Collections.Generic.List<ComboEntry>(explicitTable.Entries);
            foreach (var entry in Entries ?? Array.Empty<ComboEntryAsset>())
            {
                if (Array.IndexOf(skills, entry.Skill) < 0)
                    throw new InvalidOperationException("Combo target is outside the database.");
                foreach (var pre in entry.PreSkillAssets ?? Array.Empty<BuffArenaSkillAsset>())
                    if (Array.IndexOf(skills, pre) < 0)
                        throw new InvalidOperationException("Combo prerequisite is outside the database.");
            }
            foreach (var start in GenerateStarts(skills).Entries)
                if (!combined.Exists(e => e.Input == start.Input && e.PreSkills.Length == 0 && e.RequiredTags.Length == 0))
                    combined.Add(start);
            return new ComboTableSO { Entries = combined.ToArray() };
        }
        public ComboTableSO Bake()
        {
            var source = Entries ?? Array.Empty<ComboEntryAsset>();
            var result = new ComboEntry[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var entry = source[i] ?? throw new InvalidOperationException("Combo entry " + i + " is null.");
                if (entry.Skill == null) throw new InvalidOperationException("Combo entry " + i + " has no target skill.");
                var target = entry.Skill.Bake();
                var preAssets = entry.PreSkillAssets ?? Array.Empty<BuffArenaSkillAsset>();
                var pre = new SkillNodeId[preAssets.Length];
                for (int j = 0; j < pre.Length; j++)
                {
                    if (preAssets[j] == null) throw new InvalidOperationException("Combo entry " + i + " has a null prerequisite.");
                    pre[j] = preAssets[j].Bake().Id;
                }
                if (string.IsNullOrEmpty(entry.InputAction)) throw new InvalidOperationException("Combo entry " + i + " has no input.");
                result[i] = new ComboEntry { PreSkills = pre, Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags ?? Array.Empty<int>(), Priority = entry.Priority,
                    ToSkill = target.Id, Timeline = target.Timeline };
            }
            for (int i = 0; i < result.Length; i++)
                for (int j = 0; j < i; j++)
                    if (result[i].Input == result[j].Input && result[i].Priority == result[j].Priority &&
                        Same(result[i].PreSkills, result[j].PreSkills) && Same(result[i].RequiredTags, result[j].RequiredTags))
                        throw new InvalidOperationException("Duplicate combo conditions and priority at entry " + i);
            return new ComboTableSO { Entries = result };
        }
        static bool Same<T>(T[] a, T[] b)
            => new System.Collections.Generic.HashSet<T>(a).SetEquals(b);
        public static ComboTableSO GenerateStarts(BuffArenaSkillAsset[] skills)
        {
            var entries = new System.Collections.Generic.List<ComboEntry>();
            if (skills != null) foreach (var asset in skills)
            {
                if (asset == null || string.IsNullOrEmpty(asset.InputToken)) continue;
                var skill = asset.Bake();
                entries.Add(new ComboEntry { PreSkills = Array.Empty<SkillNodeId>(), Input = skill.Input,
                    RequiredTags = Array.Empty<int>(), Priority = 0, ToSkill = skill.Id, Timeline = skill.Timeline });
            }
            return new ComboTableSO { Entries = entries.ToArray() };
        }
    }
}
