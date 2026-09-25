using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Buff Arena Skill")]
    public sealed class BuffArenaSkillAsset : ScriptableObject
    {
        public int SkillIdValue;
        // Input token name, e.g. "Fire1". Kept as a string so the mapping lives in data
        // instead of being a positional argument in code.
        public string InputToken = "";
        public int TimelineIdValue;
        public int AmmoCost;
        public string AnimatorState = "Fire";
        // Teleport bullet: the skill switches to its warp timeline once a projectile is
        // already airborne, so the flag has to be data too.
        public bool RequiresTrackedProjectile;
        // Skill cast instead while a tracked projectile is alive (teleport bullet warp).
        public int WarpSkillIdValue;
        // Skill played instead when this one cannot pay its AmmoCost (0 = none). The
        // referenced skill's own timeline is used.
        public int FallbackSkillIdValue;

        public BuffArenaSkill Bake()
        {
            return new BuffArenaSkill
            {
                Id = new SkillNodeId(SkillIdValue),
                Timeline = new TimelineId(TimelineIdValue),
                Input = new InputToken(InputToken ?? string.Empty),
                AmmoCost = AmmoCost,
                AnimatorState = AnimatorState,
                RequiresTrackedProjectile = RequiresTrackedProjectile,
                WarpSkillId = new SkillNodeId(WarpSkillIdValue),
                FallbackSkill = new SkillNodeId(FallbackSkillIdValue)
            };
        }

        void OnValidate()
        {
            if (SkillIdValue == 0) Debug.LogError("BuffArenaSkillAsset needs a SkillIdValue.", this);
            if (TimelineIdValue == 0) Debug.LogError("BuffArenaSkillAsset needs a TimelineIdValue.", this);
        }
    }
}
