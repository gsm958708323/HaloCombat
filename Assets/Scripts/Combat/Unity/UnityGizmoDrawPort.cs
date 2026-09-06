using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityGizmoDrawPort : IGizmoDrawPort
    {
        readonly Color _color;

        public UnityGizmoDrawPort(Color color)
        {
            _color = color;
        }

        public void DrawCircle(in GizmoFrame frame)
        {
            if (!frame.Visible || frame.Radius <= 0f)
                return;
            const int segments = 32;
            var center = new Vector3(frame.Center.X, frame.Center.Y + .03f, frame.Center.Z);
            var previous = center + new Vector3(frame.Radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var next =
                    center
                    + new Vector3(
                        Mathf.Cos(angle) * frame.Radius,
                        0f,
                        Mathf.Sin(angle) * frame.Radius
                    );
                Debug.DrawLine(previous, next, _color, 0f, false);
                previous = next;
            }
        }

        public void Clear() { }
    }
}
