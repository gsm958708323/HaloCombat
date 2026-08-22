using System;
using System.Linq;

namespace Combat.Core
{
    [Serializable]
    public sealed class ComboTableSO
    {
        public static ComboTableSO Create()
        {
            var combos = new ComboTableSO
            {
                Entries = new[]
                {
                    // 开招：无当前技能节点 → G1
                    new ComboEntry
                    {
                        PreSkills = Array.Empty<SkillNodeId>(),
                        Input = InputToken.Attack,
                        RequiredTags = Array.Empty<int>(),
                        Priority = 0,
                        ToSkill = SkillNodeId.G1,
                        Timeline = TimelineId.TL_G1
                    },
                    // G1 取消窗接 G2
                    new ComboEntry
                    {
                        PreSkills = new[] { SkillNodeId.G1 },
                        Input = InputToken.Attack,
                        RequiredTags = new[] { CommonTags.Cancel.Value },
                        Priority = 10,
                        ToSkill = SkillNodeId.G2,
                        Timeline = TimelineId.TL_G2
                    },
                }
            };
            return combos;
        }

        public ComboEntry[] Entries = Array.Empty<ComboEntry>();
        public bool TryResolve(
            SkillNodeId currentSkill,
            in InputToken input,
            TagComp tags,
            out ComboResolveResult result)
        {
            ComboEntry? best = null;
            int bestPri = int.MinValue;
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                if (!e.Input.Equals(input))
                    continue;
                if (!PreMatches(e.PreSkills, currentSkill))
                    continue;
                if (!TagsMatch(e.RequiredTags, tags))
                    continue;
                if (e.Priority < bestPri)
                    continue;
                best = e;
                bestPri = e.Priority;
            }
            if (best.HasValue)
            {
                var e = best.Value;
                result = new ComboResolveResult(e.ToSkill, e.Timeline, e.Priority);
                return true;
            }
            result = default;
            return false;
        }
        static bool PreMatches(SkillNodeId[] pres, SkillNodeId current)
        {
            if (pres == null || pres.Length == 0)
                return !current.IsValid; // 无前置：仅「当前没在技能节点」时可接（开招）
            for (int i = 0; i < pres.Length; i++)
            {
                if (pres[i] == current)
                    return true;
            }
            return false;
        }
        static bool TagsMatch(int[] required, TagComp tags)
        {
            if (required == null || required.Length == 0)
                return true;
            for (int i = 0; i < required.Length; i++)
            {
                if (!tags.Has(new TagId(required[i])))
                    return false;
            }
            return true;
        }
    }

    [Serializable]
    public struct ComboEntry
    {
        public SkillNodeId[] PreSkills; // 空数组或含 None 表示「从空闲也可接」的扩展点；MVP 显式写节点
        public InputToken Input;
        public int[] RequiredTags;      // TagId.Value；表级可统一注入 Cancel
        public int Priority;
        public SkillNodeId ToSkill;
        public TimelineId Timeline;
    }
    public struct Condition
    {
        public TagId Tag;
        public bool Required;

        public bool Eval(ITagRead tags)
        {
            if (Required) return tags.Has(Tag);
            return true; // 默认通过（Cancel 表级注入）
        }
    }

    public readonly struct ComboResolveResult
    {
        public readonly SkillNodeId ToSkill;
        public readonly TimelineId Timeline;
        public readonly int Priority;
        public ComboResolveResult(SkillNodeId toSkill, TimelineId timeline, int priority)
        {
            ToSkill = toSkill;
            Timeline = timeline;
            Priority = priority;
        }
    }
    // Condition 类：只读 Tag + 只读上下文（禁止改世界）
    public interface ICondition // 仅供 SO 读，MVP 可用 struct
    {
        bool Eval(ITagRead tags);
    }
}
