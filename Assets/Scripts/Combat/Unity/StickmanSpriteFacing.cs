using UnityEngine;

namespace Combat.Unity.Presentation
{
    /// <summary>Faces a 2D stickman toward the arena camera while keeping the actor root free to rotate.</summary>
    [DisallowMultipleComponent]
    public sealed class StickmanSpriteFacing : MonoBehaviour
    {
        SpriteRenderer _sprite;

        void Awake()
        {
            _sprite = GetComponentInChildren<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (_sprite == null)
                return;

            var camera = Camera.main;
            if (camera == null)
                return;

            var toCamera = camera.transform.position - _sprite.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 0.0001f)
                return;

            _sprite.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);

            var cameraRight = camera.transform.right;
            cameraRight.y = 0f;
            if (cameraRight.sqrMagnitude > 0.0001f)
                _sprite.flipX = Vector3.Dot(transform.right, cameraRight.normalized) < 0f;
        }
    }
}
