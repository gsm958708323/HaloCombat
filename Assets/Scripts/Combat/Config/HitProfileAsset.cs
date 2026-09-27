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
        public LaunchAsset Launch;
        public IFrameAsset IFrame;
        public EffectAsset[] Effects;

        public IEffect[] Bake()
        {
            var list = new List<IEffect>(4);
            foreach (var effect in Effects ?? System.Array.Empty<EffectAsset>())
                list.Add(effect != null ? effect.Bake() : null);
            if (Damage) list.Add(Damage.Bake());
            if (Stun) list.Add(Stun.Bake());
            if (Knockback) list.Add(Knockback.Bake());
            if (Launch) list.Add(Launch.Bake());
            if (IFrame) list.Add(IFrame.Bake());
            return list.ToArray();
        }

        public void ClearCache()
        {
            if (Damage) Damage.ClearCache();
            if (Stun) Stun.ClearCache();
            if (Knockback) Knockback.ClearCache();
            if (Launch) Launch.ClearCache();
            if (IFrame) IFrame.ClearCache();
            foreach (var effect in Effects ?? System.Array.Empty<EffectAsset>()) if (effect) effect.ClearCache();
        }

        void OnValidate() => ClearCache();
    }
}
