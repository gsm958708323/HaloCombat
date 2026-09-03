using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class InputAdapter
    {
        public bool EnableCombatKeys = true;
        bool _attack;
        bool _jump;
        bool _dodge;
        float _stickX;
        float _stickZ;

        public void SampleUnity()
        {
            if (EnableCombatKeys)
            {
                if (Input.GetKeyDown(KeyCode.J) || Input.GetMouseButtonDown(0)) _attack = true;
                if (Input.GetKeyDown(KeyCode.K)) _dodge = true;
            }

            if (Input.GetKeyDown(KeyCode.Space)) _jump = true;
            _stickX = Input.GetAxisRaw("Horizontal");
            _stickZ = Input.GetAxisRaw("Vertical");
        }

        public void PumpLogic(Actor actor)
        {
            if (actor == null || !actor.IsActive) return;
            if (actor.TryGetComp<LocomotionComp>(out var loco))
                loco.RequestMoveIntent(_stickX, _stickZ);
            if (!actor.TryGetComp<InputBufferComp>(out var buffer)) return;
            if (_jump) { buffer.Push(InputToken.Jump); _jump = false; }
            if (_dodge) { buffer.Push(Season2Tokens.Dodge); _dodge = false; }
            if (_attack) { buffer.Push(InputToken.Attack); _attack = false; }
        }
    }
}
