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
        bool _queuedAttack;
        bool _queuedJump;
        bool _queuedDodge;
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
            if (_jump || _queuedJump) { buffer.Push(InputToken.Jump); _jump = false; _queuedJump = false; }
            if (_dodge || _queuedDodge) { buffer.Push(Season2Tokens.Dodge); _dodge = false; _queuedDodge = false; }
            if (_attack || _queuedAttack) { buffer.Push(InputToken.Attack); _attack = false; _queuedAttack = false; }
        }

        // Test and tooling input is queued separately from Unity's sampled
        // keyboard state, so normal runtime input behavior remains unchanged.
        public void Queue(InputToken token)
        {
            if (token == InputToken.Jump) _queuedJump = true;
            else if (token == Season2Tokens.Dodge) _queuedDodge = true;
            else if (token == InputToken.Attack) _queuedAttack = true;
        }
    }
}
