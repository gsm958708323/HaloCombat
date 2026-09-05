using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Projectile")]
    public sealed class ProjectileDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Speed = 14f;
        public float Lifetime = 2f;
        public float HitRadius = 0.3f;
        public int MaxHits = 1;
        public bool SnapshotAtk = true;
        public int HostileMask;
        public int CueId;
        public float SpawnForward = 0.4f;
        public float HomingRate;
        public float HomingMaxTurn;
        public bool HomingRetarget;
        public float HomingAcquireRadius = 12f;
        public EffectAsset[] OnHit;
        public EffectAsset[] OnExpire;
        ProjectileDefinition _baked;

        public ProjectileDefinition Bake()
        {
            if (_baked != null) return _baked;
            _baked = new ProjectileDefinition
            {
                SpecId = SpecId,
                Speed = Speed,
                Lifetime = Lifetime,
                HitRadius = HitRadius,
                MaxHits = MaxHits,
                SnapshotAtk = SnapshotAtk,
                HostileMask = HostileMask,
                CueId = CueId,
                SpawnForward = SpawnForward,
                HomingRate = HomingRate,
                HomingMaxTurn = HomingMaxTurn,
                HomingRetarget = HomingRetarget,
                HomingAcquireRadius = HomingAcquireRadius,
                OnHit = BakeFx(OnHit),
                OnExpire = BakeFx(OnExpire)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            ClearFx(OnHit);
            ClearFx(OnExpire);
        }

        public static IEffect[] BakeFx(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        public static void ClearFx(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }
}
