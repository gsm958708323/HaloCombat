using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/After Damage")]
    public sealed class AfterDamageAsset : EffectAsset
    {
        public EffectAsset[] Effects;
        public bool ToSource;
        public bool RequireHostile = true;
        public bool RequireLatestCast = true;

        protected override IEffect BakeNew() => new AfterDamageEffect
        {
            Effects = ProjectileDefAsset.BakeFx(Effects),
            ToSource = ToSource,
            RequireHostile = RequireHostile,
            RequireLatestCast = RequireLatestCast
        };

        public override void ClearCache() => ProjectileDefAsset.ClearFx(Effects);
    }
}
