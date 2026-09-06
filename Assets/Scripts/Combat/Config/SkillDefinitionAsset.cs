using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Skill")]
    public sealed class SkillDefinitionAsset : ScriptableObject
    {
        public int SkillId;
        public string DisplayName;
        public SkillTimelineAsset Timeline;
        public SkillAnimationMode AnimationMode = SkillAnimationMode.Attack;
        public string InputAction;
        public float Cooldown;
        public bool CanUseInAir;
        public bool RequiresTarget;

        public SkillDefinition Bake()
        {
            if (SkillId == 0)
                throw new InvalidOperationException("SkillDefinitionAsset has no SkillId: " + name);
            if (Timeline == null)
                throw new InvalidOperationException("SkillDefinitionAsset has no Timeline: " + name);
            var timeline = Timeline.Bake();
            return new SkillDefinition
            {
                Id = new SkillNodeId(SkillId),
                Timeline = timeline.Id,
                DisplayName = string.IsNullOrEmpty(DisplayName) ? name : DisplayName,
                AnimationMode = AnimationMode
            };
        }

        public void ClearCache() => Timeline?.ClearCache();
        void OnValidate() => ClearCache();
    }
}
