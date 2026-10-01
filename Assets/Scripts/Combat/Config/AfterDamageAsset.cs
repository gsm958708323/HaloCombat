using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>仅在先前的伤害效果实际扣除目标 HP 后，按条件投递后续效果。</summary>
    [CreateAssetMenu(menuName = "Combat/Effects/After Damage")]
    public sealed class AfterDamageAsset : EffectAsset
    {
        [Tooltip("实际扣除目标 HP 且下列条件通过后，按顺序执行的效果列表。")]
        public EffectAsset[] Effects;
        [Tooltip("勾选时将效果投递给伤害来源；不勾选时投递给受击目标。")]
        public bool ToSource;
        [Tooltip("仅来源与目标属于敌对阵营时执行；任一方缺少阵营组件则不执行。")]
        public bool RequireHostile = true;
        [Tooltip("仅命中的 CastId 与来源最近一次成功施法一致时执行，防止旧弹命中授予新施法资格。")]
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
