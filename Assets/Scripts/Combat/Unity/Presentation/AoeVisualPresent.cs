using Combat.Core;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class AoeVisualPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        float _scale = 1f;

        public override void SyncLogic(CombatWorld world)
        {
            if (!Self.TryLogic(world, out var actor) || !actor.TryGetComp<AoeComp>(out var aoe))
            {
                _scale = 1f;
                return;
            }
            _scale = aoe.VisualScale;
        }

        public override void LateTick(float dt)
        {
            if (!Self.TryGet<ActorViewPresent>(out var view) || view.View == null) return;
            view.View.transform.localScale = Vector3.one * _scale;
        }

        protected override void OnDetach() => _scale = 1f;
    }
}
