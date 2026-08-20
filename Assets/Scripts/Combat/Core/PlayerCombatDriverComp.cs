namespace Combat.Core
{
    /// <summary>
    /// 玩家本地编排：不写技能规则，只接线。
    /// </summary>
     public sealed class PlayerCombatDriverComp : Comp
    {
        StateMachineComp _fsm;
        ComboComp _combo;
        SkillDirectorComp _director;
        LocomotionComp _loco;
        InputBufferComp _input;
        public override bool WantsTick => true;
        protected override void OnAttach()
        {
            _fsm = Self.GetComp<StateMachineComp>();
            _combo = Self.GetComp<ComboComp>();
            _director = Self.GetComp<SkillDirectorComp>();
            _loco = Self.GetComp<LocomotionComp>();
            _input = Self.GetComp<InputBufferComp>();
        }
        public override void Tick(float dt)
        {
            if (_fsm.Current == ActorStateId.Hit || _fsm.Current == ActorStateId.Dead)
                return;
            // Jump 输入（示例）
            if (_fsm.Current == ActorStateId.Root &&
                _input.TryPeek(out var token) &&
                token.Equals(InputToken.Jump))
            {
                _input.Consume();
                _loco.ImpulseJump();
                _fsm.TryEnter(ActorStateId.Jump, new StateEnterArgs(ActorStateId.Root, "Jump"));
                return;
            }
            if (!_combo.TryResolve(out var resolved))
                return;
            _director.Play(resolved.ToSkill, resolved.Timeline);
        }
    }
}
