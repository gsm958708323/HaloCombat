using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Character")]
    public sealed class CharacterDefinitionAsset : ScriptableObject
    {
        public string BlueprintId;
        public string DisplayName;
        public bool IsPlayer;
        public bool PlayerSelectable;
        [Tooltip("Whether this enemy runs its perception and behavior tree. Ignored for player characters.")]
        public bool EnableAI = true;
        public string ViewBlueprintId;
        public SkillDefinitionAsset[] Skills;
        public ComboTableAsset ComboTable;
        public BtNodeAsset BehaviorTree;
        public SkillDefinitionAsset DodgeSkill;
        public string DodgeInputAction = "Dodge";
        public string JumpInputAction = "Jump";
        public float AcquireRadius = 8f;
        public float AttackRange = 1.15f;
        public float FollowRange = 1.5f;
        public float LeashRange = 20f;
        public float PatrolRadius;

        public CharacterDefinition Bake(SkillCatalog skills)
        {
            if (string.IsNullOrEmpty(BlueprintId))
                throw new InvalidOperationException("CharacterDefinitionAsset has no BlueprintId: " + name);
            if (skills == null)
                throw new ArgumentNullException(nameof(skills));

            var source = Skills ?? Array.Empty<SkillDefinitionAsset>();
            var bakedSkills = new SkillDefinition[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null)
                    throw new InvalidOperationException("Character " + BlueprintId + " has an empty skill reference.");
                bakedSkills[i] = skills.Require(new SkillNodeId(source[i].SkillId));
            }

            var dodge = DodgeSkill != null ? skills.Require(new SkillNodeId(DodgeSkill.SkillId)) : null;
            return new CharacterDefinition
            {
                BlueprintId = BlueprintId,
                DisplayName = string.IsNullOrEmpty(DisplayName) ? BlueprintId : DisplayName,
                IsPlayer = IsPlayer,
                PlayerSelectable = PlayerSelectable,
                EnableAI = !IsPlayer && EnableAI,
                Skills = bakedSkills,
                Combo = ComboTable != null ? ComboTable.Bake() : null,
                BehaviorTree = BehaviorTree != null ? BehaviorTree.Bake() : null,
                PlayerCombat = IsPlayer
                    ? new PlayerCombatConfig
                    {
                        JumpInput = new InputToken(string.IsNullOrEmpty(JumpInputAction) ? "Jump" : JumpInputAction),
                        DodgeInput = new InputToken(string.IsNullOrEmpty(DodgeInputAction) ? "Dodge" : DodgeInputAction),
                        DodgeSkill = dodge != null ? dodge.Id : SkillNodeId.None
                    }
                    : null,
                AcquireRadius = AcquireRadius,
                AttackRange = AttackRange,
                FollowRange = FollowRange,
                LeashRange = LeashRange,
                PatrolRadius = PatrolRadius
            };
        }

        public void ClearCache()
        {
            if (Skills != null)
                for (int i = 0; i < Skills.Length; i++) Skills[i]?.ClearCache();
        }

        void OnValidate() => ClearCache();
    }
}
