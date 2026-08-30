using Combat.Core;

namespace Combat.Demos
{
    public static class KnockdownDemo
    {
        public static void Run()
        {
            var world = SeasonTwoDemoSupport.NewWorld();
            var attacker = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.6f, 0f);
            var fsm = attacker.GetComp<StateMachineComp>();
            var tags = attacker.GetComp<TagComp>();

            // Knockdown is a real activity, not a tag-only side effect. It owns
            // movement, clears pending player input and stops an active timeline.
            var input = attacker.GetComp<InputBufferComp>();
            input.Push(InputToken.Attack);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(attacker.GetComp<SkillDirectorComp>().IsPlaying, "skill started");
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.8f } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Knockdown && tags.Has(CommonTags.Downed), "knockdown enter");
            SeasonTwoDemoSupport.Assert(!attacker.GetComp<SkillDirectorComp>().IsPlaying, "downed hit stops skill");
            SeasonTwoDemoSupport.Assert(!input.HasBuffered, "downed clears input");
            SeasonTwoDemoSupport.Step(world, 0.1f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Knockdown, "downed blocks attack");
            world.Deliver(new IEffect[] { new HitStunEffect { Duration = 1f } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Knockdown, "hitstun cannot replace downed");
            for (int i = 0; i < 8; i++) SeasonTwoDemoSupport.Step(world, 0.1f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Root && !tags.Has(CommonTags.Downed), "knockdown recover");
            world.Deliver(new IEffect[] { new KnockdownEffect() }, target, attacker, 10f);
            fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "DeadWins" });
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Dead, "dead overrides downed");
            CombatLog.Info(CombatCategories.Knockdown, "KnockdownDemo PASSED");
        }
    }
}
