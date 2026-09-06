using Combat.Core;
using Combat.Presentation;

namespace Combat.Game
{
    public sealed class InputRouter
    {
        readonly IGameplayInputSource _src;

        public InputRouter(IGameplayInputSource s) => _src = s ?? new NullInputSource();

        public void Apply(ArenaSession s)
        {
            if (s == null)
                return;
            var f = _src.Sample();
            if (f.PausePressed)
                s.TogglePause();
            if (f.DebugToggleHitbox)
                PresentSettings.ShowHitboxes = !PresentSettings.ShowHitboxes;
            if (f.DebugToggleEnemyAI)
                s.ToggleEnemyAI();
            if (!s.AllowsGameplayInput)
                return;
            if (!s.World.TryGetActor(s.LocalPlayerId, out var p) || p == null)
                return;
            if (p.TryGetComp<LocomotionComp>(out var l))
                l.RequestMoveIntent(f.MoveX, f.MoveZ);
            if (!p.TryGetComp<InputBufferComp>(out var b))
                return;
            if (f.AttackPressed)
                b.Push(InputToken.Attack);
            if (f.Skill1Pressed)
                b.Push(InputToken.Skill1);
            if (f.Skill2Pressed)
                b.Push(InputToken.Skill2);
            if (f.Skill3Pressed)
                b.Push(InputToken.Skill3);
            if (f.JumpPressed)
                b.Push(InputToken.Jump);
            if (f.DodgePressed)
                b.Push(Season2Tokens.Dodge);
        }
    }
}
