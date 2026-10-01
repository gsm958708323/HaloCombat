using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>把持续状态定义施加到效果目标或施法者自己身上。</summary>
    [CreateAssetMenu(menuName = "Combat/Effects/ApplyDuration")]
    public sealed class ApplyDurationAsset : EffectAsset
    {
        [Tooltip("要施加的 Buff 定义；接收者由 Target 决定。")]
        public DurationSpecAsset Spec;
        [Tooltip("本次申请增加的层数；小于 1 按 1 处理，最终受 Buff 的 MaxStacks 限制。")]
        public int Stacks = 1;
        [Tooltip("目标归属：Context 挂给效果目标；Self 挂给施法者自己。玩家没有行为树，Timeline payload 的 Target 恒为 null，自增益必须用 Self。")]
        public DurationTarget Target = DurationTarget.Context;
        protected override IEffect BakeNew()
        {
            if (Spec == null) throw new System.InvalidOperationException(name + ": ApplyDuration.Spec is required.");
            return new ApplyDurationEffect(Spec.Bake(), Stacks, Target);
        }

        public override void ClearCache()
        {
            if (Spec != null) Spec.ClearCache();
        }
    }
}
