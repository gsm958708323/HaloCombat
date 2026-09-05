using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnSummon")]
    public sealed class SpawnSummonAsset : EffectAsset
    {
        public int SpecId;
        protected override IEffect BakeNew() => new SpawnSummonEffect(SpecId);
    }
}
