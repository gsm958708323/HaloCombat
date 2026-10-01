using Combat.Core;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityPresentFactory : IPresentFactory
    {
        readonly ViewPrefabTable _views;
        readonly BakedCombatData _data;
        readonly Transform _root;
        readonly Transform _vfxRoot;

        public UnityPresentFactory(ViewPrefabTable views, BakedCombatData data, Transform root, Transform vfxRoot)
        {
            _views = views;
            _data = data;
            _root = root;
            _vfxRoot = vfxRoot != null ? vfxRoot : root;
        }

        public PresentActor Create(string blueprintId)
        {
            string viewBlueprint = blueprintId;
            CharacterDefinition definition = null;
            if (_data != null && _data.Characters != null && _data.Characters.TryGet(blueprintId, out definition) &&
                !string.IsNullOrEmpty(definition.ViewBlueprintId))
                viewBlueprint = definition.ViewBlueprintId;

            if (_views == null || !_views.TryGet(viewBlueprint, out var entry))
                throw new System.InvalidOperationException("Missing view configuration for " + viewBlueprint);

            var actor = new PresentActor();
            if (entry.Kind == ViewKind.None)
                return actor;

            if (Has(entry.Features, ViewFeatures.Pose) || entry.Prefab != null)
                actor.Add(new PoseFollowPresent());
            if (entry.Prefab != null)
                actor.Add(new ActorViewPresent(entry.Prefab, _root, entry.KeepAliveOnDead,
                    entry.ModelYawOffsetDeg));
            if (entry.Kind == ViewKind.RuntimeBody && entry.Prefab != null)
                actor.Add(new AoeVisualPresent());
            if (entry.SpinDegPerSec != 0f)
                actor.Add(new ProjectileVisualPresent(entry.SpinDegPerSec));
            if (Has(entry.Features, ViewFeatures.Animation))
                actor.Add(entry.Animation == ViewAnimationProfile.Gunner
                    ? (PresentComp)new GunnerAnimationPresent()
                    : new AnimationPresent());
            if (Has(entry.Features, ViewFeatures.BuffFx))
                actor.Add(new BuffFxPresent(_vfxRoot));
            if (Has(entry.Features, ViewFeatures.Camera))
                actor.Add(new PlayerCameraPresent());
            if (Has(entry.Features, ViewFeatures.Feedback))
                actor.Add(new PlayerFeedbackPresent(_vfxRoot));
            if (Has(entry.Features, ViewFeatures.Hud))
                actor.Add(new PlayerHudSourcePresent());
            if (Has(entry.Features, ViewFeatures.HitboxGizmo))
                actor.Add(new HitboxGizmoPresent(_root));
            if (Has(entry.Features, ViewFeatures.HealthRing))
                actor.Add(new HealthRingPresent(_root));

            if (entry.Kind == ViewKind.Character && definition == null && _data != null)
            {
                throw new System.InvalidOperationException("Character view requires a character definition: " + blueprintId);
            }
            return actor;
        }

        public void Release(PresentActor actor) => actor?.Release();

        static bool Has(ViewFeatures value, ViewFeatures flag) => (value & flag) != 0;
    }
}
