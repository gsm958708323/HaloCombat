using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnAoe")]
    public sealed class SpawnAoeAsset : EffectAsset
    {
        public int SpecId;
        public bool UseTargetPoint;
        protected override IEffect BakeNew() => new SpawnAoeEffect(SpecId, UseTargetPoint);
    }
}
