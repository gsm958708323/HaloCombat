using Combat.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    /// <summary>
    /// 一帧玩家输入。键位 → 技能的映射由代码拥有，放在 BuffArenaSession.ApplyInput 里、
    /// 紧挨着它推送的技能令牌；本结构只承载 Input System 当帧采样到的值。
    /// 契约：只在本帧有效——Held 表示“这一帧按住”，Fire4 使用按下沿避免传送后重复发射，
    /// 调用方每帧重新 Sample，不要跨帧缓存或复用。
    /// AimYaw 由相机射线求得（见 BuffArenaInputSource.Sample），所以不是纯输入量：测试要断言瞄向，
    /// 必须先钉死相机与鼠标位置，否则结果随视角漂移。
    /// </summary>
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

    /// <summary>
    /// 输入采样器：绑定 Gameplay 动作图里的 9 个动作——Move、Fire1..Fire5、Jump（它施放的是 Roll 技能）、
    /// Homing、Monkey——并把手柄 / 键鼠读数折算成 BuffArenaInputFrame。
    /// 任一动作用例缺失都在构造时抛异常：场景缺件要当场响亮地失败，而不是按了半天没反应。
    /// </summary>
    public sealed class BuffArenaInputSource
    {
        // Below this |y| the camera ray is treated as parallel to the ground plane.
        const float MinGroundRaySlope = .05f;
        const float MaxAimDistance = 200f;
        const float MinAimDeltaSqr = .01f;

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

        /// <summary>解析并 Enable 动作图；_camera 允许为 null（此时 AimValid 恒为 false，只做移动/施法采样）。</summary>
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
            // Space casts Roll: the skill token is "Roll" while the action keeps its old name.
            _roll = Require(map, "Jump");
            _homing = Require(map, "Homing");
            _monkey = Require(map, "Monkey");
            _camera = camera;
            map.Enable();
        }

        /// <summary>
        /// 采样一帧。摇杆死区 0.25（sqrMagnitude &lt; .0625）直接归零，避免手抖漂移。
        /// 瞄向来自相机穿过鼠标位置的射线与地面的交点，因此依赖相机当前姿态：
        /// 射线几乎与地面平行会被拒绝（交点可能在数公里外，yaw 会剧烈抖动），距离也设有上限。
        /// </summary>
        public BuffArenaInputFrame Sample(Vector3 aimOrigin)
        {
            var move = _move.ReadValue<Vector2>();
            if (move.sqrMagnitude < .0625f) move = Vector2.zero;
            var frame = new BuffArenaInputFrame
            {
                MoveX = move.x,
                MoveZ = move.y,
                // Fire4 has a two-stage action (launch, then teleport). BuffArenaSession
                // consumes its press edge so holding the key cannot enqueue a third launch.
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
                // A ray nearly parallel to the ground plane intersects it kilometres
                // away, which makes the resulting yaw jitter. Reject those and cap
                // the usable distance instead of trusting the raw hit point.
                if (ray.direction.y < -MinGroundRaySlope)
                {
                    float distance = -ray.origin.y / ray.direction.y;
                    if (distance >= 0f && distance <= MaxAimDistance)
                    {
                        Vector3 point = ray.origin + ray.direction * distance;
                        Vector3 delta = point - aimOrigin;
                        delta.y = 0f;
                        if (delta.sqrMagnitude > MinAimDeltaSqr)
                        {
                            frame.AimYaw = LocomotionComp.YawFromStick(new SimVec3(delta.x, 0f, delta.z));
                            frame.AimValid = true;
                        }
                    }
                }
            }
            return frame;
        }

        /// <summary>按名字取动作，取不到即抛；动作名与 BuffArenaSession.ApplyInput 的技能令牌是两套东西，别混淆。</summary>
        static InputAction Require(InputActionMap map, string name)
        {
            var action = map.FindAction(name, false);
            if (action == null)
                throw new System.InvalidOperationException("InputActionAsset is missing action '" + name + "'.");
            return action;
        }
    }
}
