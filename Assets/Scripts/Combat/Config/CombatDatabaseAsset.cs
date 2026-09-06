using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Database")]
    public sealed class CombatDatabaseAsset : ScriptableObject
    {
        public ComboTableAsset Combo;
        public SkillDefinitionAsset[] Skills;
        public CharacterDefinitionAsset[] Characters;
        public SkillTimelineAsset[] Timelines;
        public ProjectileDefAsset[] Projectiles;
        public AoeDefAsset[] Aoes;
        public DurationSpecAsset[] Buffs;
        public SummonDefAsset[] Summons;
        public CueLibraryAsset Cues;
        public CharacterMotorAsset Motor;

        public void ClearCache()
        {
            ClearArray(Timelines, item => item.ClearCache());
            ClearArray(Skills, item => item.ClearCache());
            ClearArray(Characters, item => item.ClearCache());
            ClearArray(Projectiles, item => item.ClearCache());
            ClearArray(Aoes, item => item.ClearCache());
            ClearArray(Buffs, item => item.ClearCache());
            ClearArray(Summons, item => item.ClearCache());
        }

        public BakedCombatData BakeAll()
        {
            ClearCache();
            if (Skills == null || Skills.Length == 0)
                throw new InvalidOperationException("CombatDatabase requires Skills.");
            if (Characters == null || Characters.Length == 0)
                throw new InvalidOperationException("CombatDatabase requires Characters.");
            var data = new BakedCombatData
            {
                Combo = Combo != null ? Combo.Bake() : null,
                Timelines = new TimelineLibrary(),
                Skills = new SkillCatalog(),
                Characters = new CharacterCatalog(),
                Projectiles = new ProjectileCatalog(),
                Aoes = new AoeCatalog(),
                Summons = new SummonCatalog(),
                Cues = Cues != null ? Cues.Bake() : CueLibrary.DefaultCombat(),
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults()
            };
            for (int i = 0; i < Skills.Length; i++)
            {
                if (Skills[i] == null)
                    throw new InvalidOperationException("CombatDatabase contains an empty skill reference at " + i + ".");
                var skill = Skills[i].Bake();
                data.Skills.Register(skill);
                data.Timelines.Register(Skills[i].Timeline.Bake());
            }
            for (int i = 0; i < Characters.Length; i++)
            {
                if (Characters[i] == null)
                    throw new InvalidOperationException("CombatDatabase contains an empty character reference at " + i + ".");
                data.Characters.Register(Characters[i].Bake(data.Skills));
            }
            if (Timelines != null)
                for (int i = 0; i < Timelines.Length; i++) if (Timelines[i]) data.Timelines.Register(Timelines[i].Bake());
            if (Projectiles != null)
                for (int i = 0; i < Projectiles.Length; i++) if (Projectiles[i]) data.Projectiles.Register(Projectiles[i].Bake());
            if (Aoes != null)
                for (int i = 0; i < Aoes.Length; i++) if (Aoes[i]) data.Aoes.Register(Aoes[i].Bake());
            if (Summons != null)
                for (int i = 0; i < Summons.Length; i++) if (Summons[i]) data.Summons.Register(Summons[i].Bake());
            return data;
        }

        static void ClearArray<T>(T[] array, Action<T> clear) where T : ScriptableObject
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++) if (array[i]) clear(array[i]);
        }
    }
}
