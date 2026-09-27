using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnAoe")]
    public sealed class SpawnAoeAsset : EffectAsset
    {
        public AoeDefAsset Aoe;
        public bool UseTargetPoint;
        public float RadiusOverride;
        public float DurationOverride;
        public float ForwardOffset;
        protected override IEffect BakeNew()
        {
            if (Aoe == null) throw new InvalidOperationException(name + ": Aoe is required.");
            return new SpawnAoeEffect(Aoe.SpecId, UseTargetPoint, RadiusOverride, DurationOverride, ForwardOffset);
        }
    }
}
