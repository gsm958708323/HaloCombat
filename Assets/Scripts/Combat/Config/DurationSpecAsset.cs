using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/DurationSpec")]
    public sealed class DurationSpecAsset : ScriptableObject
    {
        public int BuffId;
        public float Duration = 3f;
        public float TickInterval;
        public int MaxStacks = 1;
        public StackPolicy Stack = StackPolicy.AddStack;
        public int MutexGroup;
        public AttrId ModAttr = AttrId.Atk;
        public ModOp ModOp = ModOp.Add;
        public float ModValue;
        public int GrantedTag;
        public ModifierAsset[] Modifiers = Array.Empty<ModifierAsset>();
        public int[] GrantedTags = Array.Empty<int>();
        public bool ClearOnHurted;
        public bool ClearOnKnockdown;
        public bool ClearOnDeath;
        public bool ClearOnManualStop;
        public EffectAsset[] OnApply;
        public EffectAsset[] OnStack;
        public EffectAsset[] OnPeriod;
        public EffectAsset[] OnExpire;
        public EffectAsset[] OnHurted;
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
        public AttrId Attr;
        public ModOp Op;
        public float Value;
    }
}
