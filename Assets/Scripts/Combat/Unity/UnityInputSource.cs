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
            var v = _move == null ? ReadKeyboardMove() : _move.ReadValue<Vector2>();
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
                AttackPressed = Pressed(_attack, Key.J),
                JumpPressed = Pressed(_jump, Key.Space),
                DodgePressed = Pressed(_dodge, Key.LeftShift),
                PausePressed = Pressed(_pause, Key.Escape),
                DebugToggleHitbox = Pressed(_gizmo, Key.F3),
            };
        }

        static Vector2 ReadKeyboardMove()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;
            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y).normalized * Mathf.Clamp01(new Vector2(x, y).magnitude);
        }

        static bool Pressed(InputAction action, Key fallback)
        {
            if (action != null)
                return action.WasPressedThisFrame();
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[fallback].wasPressedThisFrame;
        }
    }
}
