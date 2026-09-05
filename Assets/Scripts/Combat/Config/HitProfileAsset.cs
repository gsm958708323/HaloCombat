using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/HitProfile")]
    public sealed class HitProfileAsset : ScriptableObject
    {
        public DamageEffectAsset Damage;
        public HitStunAsset Stun;
        public KnockbackAsset Knockback;
        public IFrameAsset IFrame;
        IEffect[] _baked;

        public IEffect[] Bake()
        {
            if (_baked != null) return _baked;
            var list = new List<IEffect>(4);
            if (Damage) list.Add(Damage.Bake());
            if (Stun) list.Add(Stun.Bake());
            if (Knockback) list.Add(Knockback.Bake());
            if (IFrame) list.Add(IFrame.Bake());
            _baked = list.ToArray();
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            if (Damage) Damage.ClearCache();
            if (Stun) Stun.ClearCache();
            if (Knockback) Knockback.ClearCache();
            if (IFrame) IFrame.ClearCache();
        }

        void OnValidate() => ClearCache();
    }
}
