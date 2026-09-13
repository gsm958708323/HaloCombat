using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Aoe")]
    public sealed class AoeDefAsset : ScriptableObject
    {
        public int SpecId;
        public float Radius = 1.3f;
        public float Duration = 2f;
        public float PulseInterval = 0.45f;
        public bool PulseOnSpawn = true;
        public bool TrackOccupancy;
        public int HostileMask;
        public int CueId;
        public AoeMotionKind Motion;
        public float MoveSpeed;
        public bool RemoveOnObstacle;
        public bool Flying = true;
        public bool TrackProjectiles;
        public float ProjectileAbsorbForce;
        public float ProjectileRadiusScale = .05f;
        public string ViewBlueprintId;
        public EffectAsset[] OnPulse;
        public EffectAsset[] OnEnter;
        public EffectAsset[] OnExit;
        public EffectAsset[] OnStay;
        public EffectAsset[] OnExpire;
        AoeDefinition _baked;

        public AoeDefinition Bake()
        {
            if (_baked != null) return _baked;
            _baked = new AoeDefinition
            {
                SpecId = SpecId,
                Radius = Radius,
                Duration = Duration,
                PulseInterval = PulseInterval,
                PulseOnSpawn = PulseOnSpawn,
                TrackOccupancy = TrackOccupancy,
                HostileMask = HostileMask,
                CueId = CueId,
                Motion = Motion,
                MoveSpeed = MoveSpeed,
                RemoveOnObstacle = RemoveOnObstacle,
                Flying = Flying,
                TrackProjectiles = TrackProjectiles,
                ProjectileAbsorbForce = ProjectileAbsorbForce,
                ProjectileRadiusScale = ProjectileRadiusScale,
                ViewBlueprintId = ViewBlueprintId,
                OnPulse = ProjectileDefAsset.BakeFx(OnPulse),
                OnEnter = ProjectileDefAsset.BakeFx(OnEnter),
                OnExit = ProjectileDefAsset.BakeFx(OnExit),
                OnStay = ProjectileDefAsset.BakeFx(OnStay),
                OnExpire = ProjectileDefAsset.BakeFx(OnExpire)
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            ProjectileDefAsset.ClearFx(OnPulse);
            ProjectileDefAsset.ClearFx(OnEnter);
            ProjectileDefAsset.ClearFx(OnExit);
            ProjectileDefAsset.ClearFx(OnStay);
            ProjectileDefAsset.ClearFx(OnExpire);
        }

        void OnValidate() => ClearCache();
    }
}
