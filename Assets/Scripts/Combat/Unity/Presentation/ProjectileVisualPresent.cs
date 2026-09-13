using Combat.Core;
using UnityEngine;

namespace Combat.Presentation
{
    /// Visual-only spin for runtime bodies whose prefab has no animation of its own.
    /// The source boomerang and spike ball both spin in flight; without this they read
    /// as frozen props sliding across the ground.
    public sealed class ProjectileVisualPresent : PresentComp
    {
        public override bool WantsLateTick => true;

        readonly float _spinDegPerSec;
        float _angle;

        public ProjectileVisualPresent(float spinDegPerSec) => _spinDegPerSec = spinDegPerSec;

        public void SetArc(float peak, float[] groundAt)
        {
            _peak = peak;
            _groundAt = groundAt;
            _elapsed = 0f;
        }

        float _peak;
        float[] _groundAt;
        float _elapsed;

        public override void LateTick(float dt)
        {
            if (!Self.TryGet<ActorViewPresent>(out var view) || view.View == null) return;
            var t = view.View.transform;

            if (_spinDegPerSec != 0f)
            {
                // The yaw convention now matches Unity (0 = +Z), so the art's own flat
                // axis is local Y and the disc/bullet spins about it.
                _angle += _spinDegPerSec * dt;
                if (_angle > 360f) _angle -= 360f;
                var e = t.localEulerAngles;
                t.localEulerAngles = new Vector3(e.x, _angle, e.z);
            }

            if (_peak > 0f)
            {
                _elapsed += dt;
                t.position = new Vector3(t.position.x, t.position.y + ArcHeight(_elapsed, _peak, _groundAt), t.position.z);
            }
        }

        /// Mirrors the source BouncingBallY: each segment peaks at peak / 2^i and the
        /// body is only ever lifted, never pushed below the logic plane.
        static float ArcHeight(float t, float peak, float[] groundAt)
        {
            if (groundAt == null || groundAt.Length == 0) return 0f;
            float start = 0f;
            for (int i = 0; i < groundAt.Length; i++)
            {
                float end = groundAt[i];
                if (t <= end)
                {
                    float span = Mathf.Max(.001f, end - start);
                    float u = Mathf.Clamp01((t - start) / span);
                    float h = Mathf.Sin(u * Mathf.PI) * (peak / Mathf.Pow(2f, i));
                    return Mathf.Max(0f, h);
                }
                start = end;
            }
            return 0f;
        }
    }
}
