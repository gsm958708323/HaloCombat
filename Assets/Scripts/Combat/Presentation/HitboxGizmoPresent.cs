using Combat.Core;

namespace Combat.Presentation
{
    public enum GizmoKind : byte
    {
        None,
        Hitbox,
        Projectile,
        Aoe,
    }

    public struct GizmoFrame
    {
        public bool Visible;
        public GizmoKind Kind;
        public SimVec3 Center;
        public float Radius;
    }

    public interface IGizmoDrawPort
    {
        void DrawCircle(in GizmoFrame frame);
        void Clear();
    }

    public sealed class NullGizmoDrawPort : IGizmoDrawPort
    {
        public void DrawCircle(in GizmoFrame f) { }

        public void Clear() { }
    }

    public sealed class HitboxGizmoPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public GizmoFrame Frame { get; private set; }
        public IGizmoDrawPort Port
        {
            get => _port;
            set => _port = value ?? new NullGizmoDrawPort();
        }
        IGizmoDrawPort _port = new NullGizmoDrawPort();

        public override void SyncLogic(CombatWorld world)
        {
            Frame = default(GizmoFrame);
            if (!PresentSettings.ShowHitboxes || !Self.TryLogic(world, out var a))
                return;
            if (
                a.TryGetComp<HitboxComp>(out var box)
                && box.IsOpen
                && a.TryGetComp<TransformComp>(out var tf)
            )
            {
                Frame = new GizmoFrame
                {
                    Visible = true,
                    Kind = GizmoKind.Hitbox,
                    Center = CombatGeom.HitboxCenter(tf, box),
                    Radius = box.Radius,
                };
                return;
            }
            if (
                a.TryGetComp<ProjectileComp>(out var p)
                && p.Def != null
                && a.TryGetComp<TransformComp>(out var pt)
            )
                Frame = new GizmoFrame
                {
                    Visible = true,
                    Kind = GizmoKind.Projectile,
                    Center = pt.Position,
                    Radius = p.Def.HitRadius,
                };
            else if (
                a.TryGetComp<AoeComp>(out var ao)
                && ao.Def != null
                && a.TryGetComp<TransformComp>(out var at)
            )
                Frame = new GizmoFrame
                {
                    Visible = true,
                    Kind = GizmoKind.Aoe,
                    Center = at.Position,
                    Radius = ao.Def.Radius,
                };
        }

        public override void LateTick(float dt)
        {
            if (!PresentSettings.ShowHitboxes || !Frame.Visible)
                _port.Clear();
            else
                _port.DrawCircle(Frame);
        }

        protected override void OnDetach()
        {
            _port.Clear();
            Frame = default(GizmoFrame);
        }
    }
}
