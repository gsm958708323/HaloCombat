using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnProjectile")]
    public sealed class SpawnProjectileAsset : EffectAsset
    {
        public int SpecId;
        protected override IEffect BakeNew() => new SpawnProjectileEffect(SpecId);
    }
}
