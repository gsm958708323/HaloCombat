using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Damage")]
    public sealed class DamageEffectAsset : EffectAsset
    {
        public float Coeff = 1f;
        public float Flat;
        public bool IgnoreDef;
        public bool CanCrit = true;
        public bool UseSnapshotAtk = true;
        public bool ScaleByBuffStacks;
        public int HitstopFrames;
        public float CritMul = 2f;
        // -1 keeps the source actor's CritRate; >= 0 pins the roll for this effect.
        public float CritChance = -1f;
        public bool DirectDamage = true;
        public bool FireOnHurted = true;

        protected override IEffect BakeNew() => new DamageEffect
        {
            Coeff = Coeff,
            Flat = Flat,
            IgnoreDef = IgnoreDef,
            CanCrit = CanCrit,
            UseSnapshotAtk = UseSnapshotAtk,
            ScaleByBuffStacks = ScaleByBuffStacks,
            HitstopFrames = HitstopFrames,
            CritMul = CritMul,
            CritChance = CritChance,
            DirectDamage = DirectDamage,
            FireOnHurted = FireOnHurted
        };
    }
}
