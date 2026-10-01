using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/IFrame")]
    public sealed class IFrameAsset : EffectAsset
    {
        public float Duration = 0.1f;
        protected override IEffect BakeNew() => new IFrameEffect { Duration = Duration };
    }
}
