using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/ApplyDuration")]
    public sealed class ApplyDurationAsset : EffectAsset
    {
        public DurationSpecAsset Spec;
        public int Stacks = 1;
        protected override IEffect BakeNew() => Spec == null
            ? new ApplyDurationEffect(null, Stacks)
            : new ApplyDurationEffect(Spec.Bake(), Stacks);

        public override void ClearCache()
        {
            if (Spec != null) Spec.ClearCache();
        }
    }
}
