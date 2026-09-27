using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>把持续状态定义施加到效果上下文的目标身上。</summary>
    [CreateAssetMenu(menuName = "Combat/Effects/ApplyDuration")]
    public sealed class ApplyDurationAsset : EffectAsset
    {
        [Tooltip("要施加的 Buff 定义；接收者是当前效果的目标。")]
        public DurationSpecAsset Spec;
        [Tooltip("本次申请增加的层数；小于 1 按 1 处理，最终受 Buff 的 MaxStacks 限制。")]
        public int Stacks = 1;
        protected override IEffect BakeNew()
        {
            if (Spec == null) throw new System.InvalidOperationException(name + ": ApplyDuration.Spec is required.");
            return new ApplyDurationEffect(Spec.Bake(), Stacks);
        }

        public override void ClearCache()
        {
            if (Spec != null) Spec.ClearCache();
        }
    }
}
