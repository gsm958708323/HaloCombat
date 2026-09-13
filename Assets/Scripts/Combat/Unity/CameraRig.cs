using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Pivot;
        public Camera Cam;
        public float Distance = 6f,
            Height = 5.5f,
            BaseFov = 50f;
        public bool ScreenShakeEnabled = true;

        public void SetScreenShake(bool enabled)
        {
            ScreenShakeEnabled = enabled;
        }

        public void Apply(PresentHub hub)
        {
            if (hub == null || !hub.TryGetLocalCamera(out var c))
                return;
            if (!c.HasFocus)
                return;
            var p = c.Focus.Spring;
            var s = new Vector3(p.X, p.Y, p.Z);
            if (Cam != null)
            {
                var offset = new Vector3(0f, 0f, -Distance);
                var shake = ScreenShakeEnabled && c.Focus.Impulse > 0f
                    ? new Vector3(Mathf.Sin(Time.unscaledTime * 70f), Mathf.Cos(Time.unscaledTime * 61f), 0f) * c.Focus.Impulse * .045f
                    : Vector3.zero;
                Cam.transform.position = s + offset + Vector3.up * Height + shake;
                Cam.transform.LookAt(s);
                Cam.fieldOfView = BaseFov + (ScreenShakeEnabled ? c.Focus.Impulse * 2.5f : 0f);
            }
            else if (Pivot != null)
                Pivot.position = s;
        }
    }
}
