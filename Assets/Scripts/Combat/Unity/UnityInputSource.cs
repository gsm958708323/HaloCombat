using Combat.Game;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    public sealed class UnityInputSource : IGameplayInputSource
    {
        readonly InputAction _move,
            _attack,
            _skill1,
            _skill2,
            _skill3,
            _jump,
            _dodge,
            _pause,
            _gizmo;
        readonly Transform _cam;

        public UnityInputSource(InputActionAsset asset, Transform cameraYaw)
        {
            if (asset == null)
                throw new System.InvalidOperationException("Gameplay InputActionAsset is required.");
            var gp = asset.FindActionMap("Gameplay", false);
            if (gp == null)
                throw new System.InvalidOperationException("InputActionAsset is missing the Gameplay action map.");
            var dbg = asset.FindActionMap("Debug", false);
            _move = Require(gp, "Move");
            _attack = Require(gp, "Attack");
            _skill1 = Require(gp, "Skill1");
            _skill2 = Require(gp, "Skill2");
            _skill3 = Require(gp, "Skill3");
            _jump = Require(gp, "Jump");
            _dodge = Require(gp, "Dodge");
            _pause = Require(gp, "Pause");
            _gizmo = dbg != null ? Require(dbg, "Hitbox") : null;
            _cam = cameraYaw;
            gp.Enable();
            dbg?.Enable();
        }

        public GameplayInputFrame Sample()
        {
            var v = _move.ReadValue<Vector2>();
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
                AttackPressed = _attack.WasPressedThisFrame(),
                Skill1Pressed = _skill1.WasPressedThisFrame(),
                Skill2Pressed = _skill2.WasPressedThisFrame(),
                Skill3Pressed = _skill3.WasPressedThisFrame(),
                JumpPressed = _jump.WasPressedThisFrame(),
                DodgePressed = _dodge.WasPressedThisFrame(),
                PausePressed = _pause.WasPressedThisFrame(),
                DebugToggleHitbox = _gizmo != null && _gizmo.WasPressedThisFrame(),
            };
        }

        static InputAction Require(InputActionMap map, string name)
        {
            var action = map.FindAction(name, false);
            if (action == null)
                throw new System.InvalidOperationException("InputActionAsset is missing action '" + name + "'.");
            return action;
        }
    }
}
