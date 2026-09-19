using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    // Own file on purpose. Unity only mints a MonoScript for the class whose name matches
    // its file name; a ScriptableObject class without one serialises as m_Script: 0 and
    // deserialises to null after a domain reload.
    [CreateAssetMenu(menuName = "Combat/Effects/Barrel Explosion")]
    public sealed class BarrelExplosionAsset : EffectAsset
    {
        public float Radius = 2.2f;
        public float DamageCoeff = .15f;
        [Tooltip("Team id treated as the enemy side when applying the blast.")]
        public int EnemyTeamId = 2;
        protected override IEffect BakeNew() => new BarrelExplosionEffect
        {
            Radius = Radius,
            DamageCoeff = DamageCoeff,
            EnemyTeamId = EnemyTeamId
        };
    }
}
