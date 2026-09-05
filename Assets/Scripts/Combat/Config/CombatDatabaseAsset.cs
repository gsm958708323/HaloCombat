using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Database")]
    public sealed class CombatDatabaseAsset : ScriptableObject
    {
        public ComboTableAsset Combo;
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
            ClearArray(Projectiles, item => item.ClearCache());
            ClearArray(Aoes, item => item.ClearCache());
            ClearArray(Buffs, item => item.ClearCache());
            ClearArray(Summons, item => item.ClearCache());
        }

        public BakedCombatData BakeAll()
        {
            ClearCache();
            var data = new BakedCombatData
            {
                Combo = Combo != null ? Combo.Bake() : DemoTables.G1G2(),
                Timelines = new TimelineLibrary(),
                Projectiles = new ProjectileCatalog(),
                Aoes = new AoeCatalog(),
                Summons = new SummonCatalog(),
                Cues = Cues != null ? Cues.Bake() : CueLibrary.DefaultCombat(),
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults()
            };
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
