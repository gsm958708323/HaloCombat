using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/PlaySkill")]
    public sealed class ActPlaySkillAsset : BtNodeAsset
    {
        public SkillDefinitionAsset Skill;
        public int SkillId = SkillNodeId.G1.Value;
        public int TimelineId = Combat.Core.TimelineId.TL_G1.Value;
        public override BtNode Bake()
        {
            if (Skill != null)
            {
                var definition = Skill.Bake();
                return new ActPlaySkill(definition.Id);
            }
            return new ActPlaySkill(new SkillNodeId(SkillId), new Combat.Core.TimelineId(TimelineId));
        }
    }
}
