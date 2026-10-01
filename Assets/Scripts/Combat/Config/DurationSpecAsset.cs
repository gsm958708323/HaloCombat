using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>Buff 的配置定义：控制持续时间、叠层、属性、标签和生命周期效果。</summary>
    [CreateAssetMenu(menuName = "Combat/DurationSpec")]
    public sealed class DurationSpecAsset : ScriptableObject
    {
        [Tooltip("Buff 的非零唯一 ID，与授予的 Tag ID 是两种不同身份。")]
        public int BuffId;
        [Tooltip("持续的角色逻辑时间，单位为秒；0 表示不会因时间自动到期。")]
        public float Duration = 3f;
        [Tooltip("大于 0 时按此间隔执行 OnPeriod；0 表示不周期触发。")]
        public float TickInterval;
        [Tooltip("单个 Buff 实例允许的最大层数；至少为 1。")]
        public int MaxStacks = 1;
        [Tooltip("同 BuffId 再次施加时：刷新时长、增加层数、创建独立实例或拒绝重复。")]
        public StackPolicy Stack = StackPolicy.AddStack;
        [Tooltip("大于 0 时与同组的其他 BuffId 互斥；0 表示不互斥。")]
        public int MutexGroup;
        [Tooltip("单项属性修改的属性类型；仅 ModValue 非零时生效。")]
        public AttrId ModAttr = AttrId.Atk;
        [Tooltip("单项属性修改的计算方式；仅 ModValue 非零时生效。")]
        public ModOp ModOp = ModOp.Add;
        [Tooltip("单项属性修改的数值；0 表示不添加该项，可改用 Modifiers 列表。")]
        public float ModValue;
        [Tooltip("单项授予的 Tag ID；0 表示不使用，并与 GrantedTags 列表合并。")]
        public int GrantedTag;
        [Tooltip("额外属性修改；Buff 移除时一并撤销。与上面的单项属性修改合并。")]
        public ModifierAsset[] Modifiers = Array.Empty<ModifierAsset>();
        [Tooltip("Buff 存在期间授予的 Tag ID 列表；移除时释放，与 GrantedTag 合并。")]
        public int[] GrantedTags = Array.Empty<int>();
        [Tooltip("受击导致技能以 Hit 原因停止时清除；配置 OnHurted 时也可在受击回调后清除。")]
        public bool ClearOnHurted;
        [Tooltip("技能以 Knockdown 原因停止时清除。")]
        public bool ClearOnKnockdown;
        [Tooltip("技能以 Dead 原因停止时清除。")]
        public bool ClearOnDeath;
        [Tooltip("技能被手动停止时清除；自然播完不会因此清除。")]
        public bool ClearOnManualStop;
        [Tooltip("首次创建此 Buff 实例时执行的效果。")]
        public EffectAsset[] OnApply;
        [Tooltip("AddStack 使已有 Buff 层数增加时执行；留空则回用 OnApply。")]
        public EffectAsset[] OnStack;
        [Tooltip("每达到 TickInterval 间隔时执行的效果。")]
        public EffectAsset[] OnPeriod;
        [Tooltip("到期、驱散或正常清除时执行；实体卸载时的静默清理不会触发。")]
        public EffectAsset[] OnExpire;
        [Tooltip("持有者受击时执行的效果；本次受击的攻击者作为效果来源。")]
        public EffectAsset[] OnHurted;
        [Tooltip("持有者成功开始施法时执行的效果。")]
        public EffectAsset[] OnOwnerCast;
        public DurationSpec Bake()
        {
            return new DurationSpec
            {
                BuffId = BuffId,
                Duration = Duration,
                TickInterval = TickInterval,
                MaxStacks = MaxStacks,
                Stack = Stack,
                MutexGroup = MutexGroup,
                Modifiers = BakeModifiers(),
                GrantedTags = BakeTags(),
                ClearOnHurted = ClearOnHurted,
                ClearOnKnockdown = ClearOnKnockdown,
                ClearOnDeath = ClearOnDeath,
                ClearOnManualStop = ClearOnManualStop,
                OnApply = BakeList(OnApply),
                OnStack = BakeList(OnStack),
                OnPeriod = BakeList(OnPeriod),
                OnExpire = BakeList(OnExpire),
                OnHurted = BakeList(OnHurted),
                OnOwnerCast = BakeList(OnOwnerCast)
            };
        }

        Modifier[] BakeModifiers()
        {
            var list = new System.Collections.Generic.List<Modifier>();
            if (ModValue != 0f) list.Add(new Modifier { Attr = ModAttr, Op = ModOp, Value = ModValue });
            foreach (var item in Modifiers ?? Array.Empty<ModifierAsset>())
                if (item != null) list.Add(new Modifier { Attr = item.Attr, Op = item.Op, Value = item.Value });
            return list.ToArray();
        }

        TagId[] BakeTags()
        {
            var list = new System.Collections.Generic.List<TagId>();
            if (GrantedTag != 0) list.Add(new TagId(GrantedTag));
            foreach (var tag in GrantedTags ?? Array.Empty<int>()) if (tag != 0) list.Add(new TagId(tag));
            return list.ToArray();
        }

        public void ClearCache()
        {
            ClearList(OnApply);
            ClearList(OnStack);
            ClearList(OnPeriod);
            ClearList(OnExpire);
            ClearList(OnHurted);
            ClearList(OnOwnerCast);
        }

        static IEffect[] BakeList(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        static void ClearList(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }

    [Serializable]
    public sealed class ModifierAsset
    {
        [Tooltip("要修改的属性。")]
        public AttrId Attr;
        [Tooltip("属性修改的计算方式。")]
        public ModOp Op;
        [Tooltip("属性修改的数值。")]
        public float Value;
    }
}
