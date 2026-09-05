using Combat.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    public sealed class UnityInputSource : IGameplayInputSource
    {
        readonly InputAction _move,
            _attack,
            _jump,
            _dodge,
            _pause,
            _gizmo;
        readonly Transform _cam;

        public UnityInputSource(InputActionAsset asset, Transform cameraYaw)
        {
            var gp =
                asset == null
                    ? null
                    : asset.FindActionMap("Gameplay", false)
                        ?? asset.FindActionMap("Player", false);
            _move = gp?.FindAction("Move", false);
            _attack = gp?.FindAction("Attack", false);
            _jump = gp?.FindAction("Jump", false);
            _dodge = gp?.FindAction("Dodge", false);
            _pause = gp?.FindAction("Pause", false);
            var dbg = asset?.FindActionMap("Debug", false);
            _gizmo = dbg?.FindAction("Hitbox", false);
            _cam = cameraYaw;
            gp?.Enable();
            dbg?.Enable();
        }

        public GameplayInputFrame Sample()
        {
            var v = _move == null ? Vector2.zero : _move.ReadValue<Vector2>();
            float x = v.x,
                z = v.y;
            if (x * x + z * z < .0625f)
            {
                x = 0;
                z = 0;
            }
            else if (_cam != null)
            {
                var f = _cam.forward;
                f.y = 0;
                var r = _cam.right;
                r.y = 0;
                if (f.sqrMagnitude < 1e-6f)
                    f = Vector3.forward;
                if (r.sqrMagnitude < 1e-6f)
                    r = Vector3.right;
                f.Normalize();
                r.Normalize();
                var w = r * x + f * z;
                x = w.x;
                z = w.z;
            }
            return new GameplayInputFrame
            {
                MoveX = x,
                MoveZ = z,
                AttackPressed = _attack != null && _attack.WasPressedThisFrame(),
                JumpPressed = _jump != null && _jump.WasPressedThisFrame(),
                DodgePressed = _dodge != null && _dodge.WasPressedThisFrame(),
                PausePressed = _pause != null && _pause.WasPressedThisFrame(),
                DebugToggleHitbox = _gizmo != null && _gizmo.WasPressedThisFrame(),
            };
        }
    }
}
