using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Launch")]
    public sealed class LaunchAsset : EffectAsset
    {
        public float VerticalSpeed = 5f;
        protected override IEffect BakeNew() => new LaunchEffect { VerticalSpeed = VerticalSpeed };
    }
}
