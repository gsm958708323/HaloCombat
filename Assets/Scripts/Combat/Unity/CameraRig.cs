using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Pivot;
        public Camera Cam;
        public float Distance = 6f,
            Height = .5f,
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
            var p = c.Focus.Spring;
            var s = new Vector3(p.X, p.Y, p.Z);
            if (Cam != null)
            {
                Cam.transform.position = s - Vector3.forward * Distance;
                Cam.transform.LookAt(s);
                Cam.fieldOfView = BaseFov + (ScreenShakeEnabled ? c.Focus.Impulse * 3f : 0f);
            }
            else if (Pivot != null)
                Pivot.position = s;
        }
    }
}
