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
        public EffectAsset[] OnApply;
        public EffectAsset[] OnStack;
        public EffectAsset[] OnPeriod;
        public EffectAsset[] OnExpire;
        public EffectAsset[] OnHurted;
        public EffectAsset[] OnOwnerCast;
        DurationSpec _baked;

        public DurationSpec Bake()
        {
            if (_baked != null) return _baked;
            _baked = new DurationSpec
            {
                BuffId = BuffId,
                Duration = Duration,
                TickInterval = TickInterval,
                MaxStacks = MaxStacks,
                Stack = Stack,
                MutexGroup = MutexGroup,
                Modifiers = ModValue != 0f
                    ? new[] { new Modifier { Attr = ModAttr, Op = ModOp, Value = ModValue } }
                    : Array.Empty<Modifier>(),
                GrantedTags = GrantedTag != 0
                    ? new[] { new TagId(GrantedTag) }
                    : Array.Empty<TagId>(),
                OnApply = BakeList(OnApply),
                OnStack = BakeList(OnStack),
                OnPeriod = BakeList(OnPeriod),
                OnExpire = BakeList(OnExpire),
                OnHurted = BakeList(OnHurted),
                OnOwnerCast = BakeList(OnOwnerCast)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
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
}
