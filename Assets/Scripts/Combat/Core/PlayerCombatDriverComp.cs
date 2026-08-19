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

        public override bool WantsTick => true;

        protected override void OnAttach()
        {
            _fsm = Self.GetComp<StateMachineComp>();
            _combo = Self.GetComp<ComboComp>();
            _director = Self.GetComp<SkillDirectorComp>();
        }

        protected override void OnDetach()
        {
            _fsm = null;
            _combo = null;
            _director = null;
        }

        public override void Tick(float dt)
        {
            if (_fsm.Current == ActorStateId.Hit || _fsm.Current == ActorStateId.Dead)
                return;

            if (!_combo.TryResolve(out var resolved))
                return;

            if (_fsm.Current != ActorStateId.Attack)
                _fsm.TryEnter(ActorStateId.Attack, new StateEnterArgs(_fsm.Current, "Combo"));

            _director.Play(resolved.ToSkill, resolved.Timeline);
        }
    }
}
