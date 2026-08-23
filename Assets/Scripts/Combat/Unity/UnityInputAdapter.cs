using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityInputAdapter : MonoBehaviour
    {
        DemoCombatSession _session;

        public void Bind(DemoCombatSession session) => _session = session;

        public void Pump()
        {
            if (_session == null) return;
            if (!_session.World.TryGetActor(_session.PlayerId, out var player)) return;

            var input = player.GetComp<InputBufferComp>();
            var loco = player.GetComp<LocomotionComp>();

            // 移动意图（Root/Jump 生效；Attack 被 Locomotion 忽略）
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
            loco.SetMoveIntent(x, z);

            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Mouse0))
                input.Push(InputToken.Attack);

            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Space))
                input.Push(InputToken.Jump);

            // 调试：手动受击（验 Clear + Stop + 回 Root）
            if (Input.GetKeyDown(KeyCode.H))
            {
                var fsm = player.GetComp<StateMachineComp>();
                fsm.TryEnter(ActorStateId.Hit, new StateEnterArgs(fsm.Current, "DebugHit"));
            }
        }
    }
}
