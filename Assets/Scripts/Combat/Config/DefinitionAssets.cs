using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [Serializable]
    public sealed class TimelineClipAsset
    {
        public float Start;
        public float End;
        public ClipKind Kind;
        public float MoveX;
        public float MoveY;
        public float MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX;
        public float HitOffsetY;
        public float HitOffsetZ;
        public HitProfileAsset HitProfile;
    }

    [Serializable]
    public sealed class TimelinePayloadAsset
    {
        public float Time;
        public EffectAsset[] Effects;
    }

    [Serializable]
    public sealed class ComboEntryAsset
    {
        public int[] PreSkills;
        public string InputAction = "Attack";
        public int[] RequiredTags;
        public int Priority;
        public int ToSkill;
        public int Timeline;
    }

    public sealed class SoCombatContent : ICombatContent
    {
        readonly CombatDatabaseAsset _database;

        public SoCombatContent(CombatDatabaseAsset database) => _database = database;
        public BakedCombatData Bake() => _database != null ? _database.BakeAll() : new CodeCombatContent().Bake();
    }

    public enum TreeRecipeKind { None, Puncher, Guard, SummonMelee }

    public static class TreeRecipe
    {
        public static BtNode Build(TreeRecipeKind kind)
        {
            switch (kind)
            {
                case TreeRecipeKind.Puncher: return BtFactory.MeleePuncher(SkillNodeId.G1, TimelineId.TL_G1);
                case TreeRecipeKind.Guard: return BtFactory.MeleeGuard(SkillNodeId.G1, TimelineId.TL_G1);
                case TreeRecipeKind.SummonMelee: return BtFactory.SummonMelee(SkillNodeId.G1, TimelineId.TL_G1);
                default: return null;
            }
        }
    }
}
