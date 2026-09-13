using Combat.Core;
using UnityEngine;

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

    public sealed class HitboxGizmoPresent : PresentComp
    {
        const int Segments = 32;
        readonly Transform _root;
        GameObject _object;
        LineRenderer _line;
        Material _material;
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public GizmoFrame Frame { get; private set; }

        public HitboxGizmoPresent(Transform root)
        {
            _root = root;
        }

        protected override void OnAttach()
        {
            _object = new GameObject("CombatHitboxGizmo");
            if (_root != null)
                _object.transform.SetParent(_root, false);
            _line = _object.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.startWidth = .035f;
            _line.endWidth = .035f;
            _line.startColor = Color.red;
            _line.endColor = Color.red;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _material = new Material(shader) { color = Color.red };
                _line.sharedMaterial = _material;
            }
            _line.enabled = false;
        }

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
                    Radius = ao.Radius,
                };
        }

        public override void LateTick(float dt)
        {
            if (_line == null)
                return;
            if (!PresentSettings.ShowHitboxes || !Frame.Visible || Frame.Radius <= 0f)
            {
                _line.enabled = false;
                return;
            }
            var center = new Vector3(Frame.Center.X, Frame.Center.Y + .03f, Frame.Center.Z);
            for (int i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, center + new Vector3(
                    Mathf.Cos(angle) * Frame.Radius,
                    0f,
                    Mathf.Sin(angle) * Frame.Radius));
            }
            _line.enabled = true;
        }

        protected override void OnDetach()
        {
            if (_line != null)
                _line.enabled = false;
            if (_line != null)
                _line.sharedMaterial = null;
            Destroy(_material);
            Destroy(_object);
            _material = null;
            _line = null;
            _object = null;
            Frame = default(GizmoFrame);
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
