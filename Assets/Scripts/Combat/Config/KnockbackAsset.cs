using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Knockback")]
    public sealed class KnockbackAsset : EffectAsset
    {
        public float Distance = 0.4f;
        protected override IEffect BakeNew() => new KnockbackEffect { Distance = Distance };
    }
}
