using Combat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    public struct BuffArenaInputFrame
    {
        public float MoveX;
        public float MoveZ;
        public float AimYaw;
        public bool AimValid;
        public bool Fire1Held;
        public bool Fire2Held;
        public bool Fire3Held;
        public bool Fire4Held;
        public bool Fire5Held;
        public bool RollHeld;
        public bool HomingHeld;
        public bool MonkeyHeld;
    }

    public sealed class BuffArenaInputSource
    {
        readonly InputAction _move;
        readonly InputAction _fire1;
        readonly InputAction _fire2;
        readonly InputAction _fire3;
        readonly InputAction _fire4;
        readonly InputAction _fire5;
        readonly InputAction _roll;
        readonly InputAction _homing;
        readonly InputAction _monkey;
        readonly Camera _camera;

        public BuffArenaInputSource(InputActionAsset asset, Camera camera)
        {
            if (asset == null) throw new System.InvalidOperationException("Buff Arena input actions are required.");
            var map = asset.FindActionMap("Gameplay", false);
            if (map == null) throw new System.InvalidOperationException("Gameplay action map is required.");
            _move = Require(map, "Move");
            _fire1 = Require(map, "Fire1");
            _fire2 = Require(map, "Fire2");
            _fire3 = Require(map, "Fire3");
            _fire4 = Require(map, "Fire4");
            _fire5 = Require(map, "Fire5");
            _roll = Require(map, "Jump");
            _homing = Require(map, "Homing");
            _monkey = Require(map, "Monkey");
            _camera = camera;
            map.Enable();
        }

        public BuffArenaInputFrame Sample(Vector3 aimOrigin)
        {
            var move = _move.ReadValue<Vector2>();
            if (move.sqrMagnitude < .0625f) move = Vector2.zero;
            var frame = new BuffArenaInputFrame
            {
                MoveX = move.x,
                MoveZ = move.y,
                Fire1Held = _fire1.IsPressed(),
                Fire2Held = _fire2.IsPressed(),
                Fire3Held = _fire3.IsPressed(),
                Fire4Held = _fire4.IsPressed(),
                Fire5Held = _fire5.IsPressed(),
                RollHeld = _roll.IsPressed(),
                HomingHeld = _homing.IsPressed(),
                MonkeyHeld = _monkey.IsPressed()
            };

            if (_camera != null && Mouse.current != null)
            {
                Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Mathf.Abs(ray.direction.y) > .0001f)
                {
                    float distance = -ray.origin.y / ray.direction.y;
                    if (distance >= 0f)
                    {
                        Vector3 point = ray.origin + ray.direction * distance;
                        Vector3 delta = point - aimOrigin;
                        delta.y = 0f;
                        if (delta.sqrMagnitude > .0001f)
                        {
                            frame.AimYaw = LocomotionComp.YawFromStick(new SimVec3(delta.x, 0f, delta.z));
                            frame.AimValid = true;
                        }
                    }
                }
            }
            return frame;
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
