using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnAoe")]
    public sealed class SpawnAoeAsset : EffectAsset
    {
        public int SpecId;
        public bool UseTargetPoint;
        public float RadiusOverride;
        public float DurationOverride;
        public float ForwardOffset;
        protected override IEffect BakeNew()
            => new SpawnAoeEffect(SpecId, UseTargetPoint, RadiusOverride, DurationOverride, ForwardOffset);
    }
}
