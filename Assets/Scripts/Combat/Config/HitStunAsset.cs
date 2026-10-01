using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/HitStun")]
    public sealed class HitStunAsset : EffectAsset
    {
        public float Duration = 0.35f;
        public float IFrameDuration;
        protected override IEffect BakeNew() => new HitStunEffect { Duration = Duration, IFrameDuration = IFrameDuration };
    }
}
