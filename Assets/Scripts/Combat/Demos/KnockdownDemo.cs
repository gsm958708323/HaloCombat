using Combat.Core;

namespace Combat.Demos
{
    // 倒地用例：验证 Knockdown 姿态的优先级、计时刷新、输入门控和死亡终态。
    public static class KnockdownDemo
    {
        public static void Run()
        {
            // 先使用纯 stake 目标验证姿态规则，再使用 fighter 验证技能和输入清理。
            var world = SeasonTwoDemoSupport.NewWorld();
            var attacker = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.6f, 0f);
            var targetFsm = target.GetComp<StateMachineComp>();
            var targetTags = target.GetComp<TagComp>();

            // 目标进入真实 Knockdown Activity，阻止攻击；重复施加只刷新计时器，
            // 不会重复累加 Downed Tag。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.5f } }, attacker, target, 10f);
            SeasonTwoDemoSupport.Assert(targetFsm.Current == ActivityId.Knockdown && targetTags.Has(CommonTags.Downed), "knockdown enter");
            SeasonTwoDemoSupport.Assert(!targetFsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "DownedAttack" }), "downed blocks attack");
            int downedStacks = targetTags.Stack(CommonTags.Downed);
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.5f } }, attacker, target, 10f);
            SeasonTwoDemoSupport.Assert(targetFsm.Current == ActivityId.Knockdown && targetTags.Stack(CommonTags.Downed) == downedStacks, "knockdown refresh");
            world.Deliver(new IEffect[] { new HitStunEffect { Duration = 1f } }, attacker, target, 10f);
            SeasonTwoDemoSupport.Assert(targetFsm.Current == ActivityId.Knockdown && !targetTags.Has(CommonTags.Stunned), "hitstun cannot replace downed");
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(targetFsm.Current == ActivityId.Root && !targetTags.Has(CommonTags.Downed), "knockdown recover");

            // Knockdown 可以覆盖已有的 Hit 姿态；倒地期间只由 Downed 姿态持有状态 Tag。
            world.Deliver(new IEffect[] { new HitStunEffect { Duration = 1f } }, attacker, target, 10f);
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, attacker, target, 10f);
            SeasonTwoDemoSupport.Assert(targetFsm.Current == ActivityId.Knockdown && !targetTags.Has(CommonTags.Stunned), "knockdown covers hit");
            for (int i = 0; i < 16; i++) SeasonTwoDemoSupport.Step(world, 0.05f);

            // 玩家倒地时清空待处理输入并停止活动 Timeline；随后 Dead 仍然拥有最高优先级。
            var fsm = attacker.GetComp<StateMachineComp>();
            var tags = attacker.GetComp<TagComp>();
            var input = attacker.GetComp<InputBufferComp>();
            input.Push(InputToken.Attack);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(attacker.GetComp<SkillDirectorComp>().IsPlaying, "skill started");
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.8f } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Knockdown && tags.Has(CommonTags.Downed), "knockdown enter");
            SeasonTwoDemoSupport.Assert(!attacker.GetComp<SkillDirectorComp>().IsPlaying, "downed hit stops skill");
            SeasonTwoDemoSupport.Assert(!input.HasBuffered, "downed clears input");
            SeasonTwoDemoSupport.Assert(!fsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "DownedAttack" }), "player downed blocks attack");
            for (int i = 0; i < 16; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            world.Deliver(new IEffect[] { new KnockdownEffect() }, target, attacker, 10f);
            fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "DeadWins" });
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Dead, "dead overrides downed");
            CombatLog.Info(CombatCategories.Knockdown, "KnockdownDemo PASSED");
        }
    }
}
