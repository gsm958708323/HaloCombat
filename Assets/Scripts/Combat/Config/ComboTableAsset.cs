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
            foreach (var entry in Entries ?? Array.Empty<ComboEntryAsset>())
            {
                if (Array.IndexOf(skills, entry.Skill) < 0)
                    throw new InvalidOperationException("Combo target is outside the database.");
                foreach (var pre in entry.PreSkillAssets ?? Array.Empty<BuffArenaSkillAsset>())
                    if (Array.IndexOf(skills, pre) < 0)
                        throw new InvalidOperationException("Combo prerequisite is outside the database.");
            }
            return explicitTable;
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
                if (string.IsNullOrWhiteSpace(entry.InputAction)) throw new InvalidOperationException("Combo entry " + i + " has no input.");
                result[i] = new ComboEntry { PreSkills = pre, Input = new InputToken(entry.InputAction),
                    RequiredTags = entry.RequiredTags == null ? Array.Empty<int>() : (int[])entry.RequiredTags.Clone(), Priority = entry.Priority,
                    ToSkill = target.Id, Timeline = target.Timeline };
            }
            for (int i = 0; i < result.Length; i++)
                for (int j = 0; j < i; j++)
                    if (result[i].Input == result[j].Input && result[i].Priority == result[j].Priority &&
                        PreOverlaps(result[i].PreSkills, result[j].PreSkills))
                        throw new InvalidOperationException(name + ".Entries[" + i + "] overlaps Entries[" + j + "]: same input, prerequisite and priority; positive RequiredTags may coexist.");
            return new ComboTableSO { Entries = result };
        }
        static bool Same<T>(T[] a, T[] b)
            => new System.Collections.Generic.HashSet<T>(a).SetEquals(b);

        static bool PreOverlaps(SkillNodeId[] a, SkillNodeId[] b)
        {
            if (a.Length == 0 || b.Length == 0) return a.Length == b.Length;
            foreach (var id in a) if (Array.IndexOf(b, id) >= 0) return true;
            return false;
        }
    }
}
