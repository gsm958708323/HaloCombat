using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Dispel")]
    public sealed class DispelAsset : EffectAsset
    {
        public DispelMode Mode;
        public int Key;
        public int TagValue;
        public int MaxCount;
        protected override IEffect BakeNew() => new DispelEffect(Mode, Key, new TagId(TagValue), MaxCount);
    }
}
