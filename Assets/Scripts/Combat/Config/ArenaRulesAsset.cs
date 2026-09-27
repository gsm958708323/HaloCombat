using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Arena Rules")]
    public sealed class ArenaRulesAsset : ScriptableObject
    {
        public int PlayerAmmoCapacity = 60;
        public int MaxEnemies = 10;
        public float SpawnPeriod = 10f;
        public float EnemyCleanupDelay = 5f;
        public float BarrelSelfDamagePeriod = 5f;
        public int Seed = 1;
        public CharacterMotorAsset Motor;
    }
}
