using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityPresentFactory : IPresentFactory
    {
        readonly ViewPrefabTable _views;
        readonly Transform _root;
        readonly Transform _vfxRoot;

        public UnityPresentFactory(ViewPrefabTable views, Transform root, Transform vfxRoot)
        {
            _views = views;
            _root = root;
            _vfxRoot = vfxRoot != null ? vfxRoot : root;
        }

        public PresentActor Create(string blueprintId)
        {
            var actor = new PresentActor();
            actor.Add(new PoseFollowPresent());
            actor.Add(new ActorViewPresent(_views != null ? _views.Find(blueprintId) : null, _root));

            bool character =
                blueprintId == "fighter"
                || blueprintId == "swordsman"
                || blueprintId == "gunslinger"
                || blueprintId == "melee_guard"
                || blueprintId == "summon"
                || blueprintId == "melee_ai"
                || blueprintId == "melee_ai_narrow"
                || blueprintId == "ranged_ai";
            if (character)
            {
                actor.Add(new AnimationPresent());
                actor.Add(new BuffFxPresent(_vfxRoot));
            }
            if (blueprintId == "fighter" || blueprintId == "swordsman" || blueprintId == "gunslinger")
            {
                actor.Add(new PlayerCameraPresent());
                actor.Add(new PlayerFeedbackPresent(_vfxRoot));
                actor.Add(new PlayerHudSourcePresent());
            }
            actor.Add(new HitboxGizmoPresent(_root));
            return actor;
        }

        public void Release(PresentActor actor) => actor?.Release();
    }
}
