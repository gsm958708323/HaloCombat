using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Buff Arena Skill")]
    public sealed class BuffArenaSkillAsset : ScriptableObject
    {
        public int SkillIdValue;
        public SkillTimelineAsset Timeline;
        public int AmmoCost;
        [Tooltip("勾选后，只有该技能已有追踪弹飞行时才进入 Warp Skill/传送分支；未勾选时 Warp Skill 不参与判定。")]
        public bool RequiresTrackedProjectile;
        [Tooltip("追踪弹仍在飞行时的传送分支引用。当前运行时用它作为传送分支配置闸门，实际位置由 TryTeleport 处理，不播放此技能的 Timeline。")]
        public BuffArenaSkillAsset WarpSkill;
        [Tooltip("当前技能无法支付 Ammo Cost 时播放的替代技能；使用被引用技能自己的 Timeline，None 表示没有缺弹回退。")]
        public BuffArenaSkillAsset FallbackSkill;

        public BuffArenaSkill Bake()
        {
            if (SkillIdValue == 0 || Timeline == null || Timeline.TimelineIdValue == 0)
                throw new InvalidOperationException(name + ": SkillIdValue and Timeline are required.");
            return new BuffArenaSkill
            {
                Id = new SkillNodeId(SkillIdValue),
                Timeline = new TimelineId(Timeline.TimelineIdValue),
                AmmoCost = AmmoCost,
                RequiresTrackedProjectile = RequiresTrackedProjectile,
                WarpSkillId = new SkillNodeId(WarpSkill != null ? WarpSkill.SkillIdValue : 0),
                FallbackSkill = new SkillNodeId(FallbackSkill != null ? FallbackSkill.SkillIdValue : 0)
            };
        }

    }
}
