using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Knockdown")]
    public sealed class KnockdownAsset : EffectAsset
    {
        public float Duration = 0.8f;
        protected override IEffect BakeNew() => new KnockdownEffect { Duration = Duration };
    }
}
