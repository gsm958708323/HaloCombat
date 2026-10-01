using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnSummon")]
    public sealed class SpawnSummonAsset : EffectAsset
    {
        public SummonDefinitionAsset Summon;
        protected override IEffect BakeNew()
        {
            if (Summon == null) throw new System.InvalidOperationException(name + ": Summon reference is required.");
            return new SpawnSummonEffect(Summon.RequireId());
        }
    }
}
