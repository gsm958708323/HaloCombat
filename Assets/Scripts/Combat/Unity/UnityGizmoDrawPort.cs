using System;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityGizmoDrawPort : IGizmoDrawPort, IDisposable
    {
        const int Segments = 32;
        readonly Color _color;
        readonly GameObject _object;
        readonly LineRenderer _line;
        readonly Material _material;

        public UnityGizmoDrawPort(Color color) : this(color, null) { }

        public UnityGizmoDrawPort(Color color, Transform parent)
        {
            _color = color;
            _object = new GameObject("CombatHitboxGizmo");
            if (parent != null)
                _object.transform.SetParent(parent, false);
            _line = _object.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.startWidth = .035f;
            _line.endWidth = .035f;
            _line.startColor = _color;
            _line.endColor = _color;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _material = new Material(shader);
                _material.color = _color;
                _line.sharedMaterial = _material;
            }
            _line.enabled = false;
        }

        public void DrawCircle(in GizmoFrame frame)
        {
            if (!frame.Visible || frame.Radius <= 0f)
            {
                Clear();
                return;
            }

            var center = new Vector3(frame.Center.X, frame.Center.Y + .03f, frame.Center.Z);
            for (int i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, center + new Vector3(
                    Mathf.Cos(angle) * frame.Radius,
                    0f,
                    Mathf.Sin(angle) * frame.Radius));
            }
            _line.enabled = true;
        }

        public void Clear()
        {
            if (_line != null)
                _line.enabled = false;
        }

        public void Dispose()
        {
            Clear();
            if (_line != null)
                _line.sharedMaterial = null;
            if (_material != null)
                Destroy(_material);
            if (_object != null)
                Destroy(_object);
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
