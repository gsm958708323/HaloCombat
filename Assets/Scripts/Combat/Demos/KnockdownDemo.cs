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
            var trace = new DemoTrace("Knockdown", CombatCategories.Knockdown, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("初始化攻击者、目标与 Knockdown 状态机", () => DemoTrace.Snapshot(target));

            // 目标进入真实 Knockdown Activity，阻止攻击；重复施加只刷新计时器，
            // 不会重复累加 Downed Tag。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.5f } }, attacker, target, 10f);
            trace.Check("目标进入 Knockdown 并获得 Downed Tag", targetFsm.Current == ActivityId.Knockdown && targetTags.Has(CommonTags.Downed),
                "Activity=Knockdown 且 Downed=true", $"Activity={targetFsm.Current} Downed={targetTags.Has(CommonTags.Downed)}",
                () => DemoTrace.Snapshot(target));
            bool downedAttack = targetFsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "DownedAttack" });
            trace.Check("倒地期间阻止 Attack", !downedAttack, "TryEnter Attack=false", $"TryEnter Attack={downedAttack}",
                () => DemoTrace.Snapshot(target));
            int downedStacks = targetTags.Stack(CommonTags.Downed);
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.5f } }, attacker, target, 10f);
            trace.Check("重复 Knockdown 只刷新计时", targetFsm.Current == ActivityId.Knockdown && targetTags.Stack(CommonTags.Downed) == downedStacks,
                "仍为 Knockdown 且层数不变", $"Activity={targetFsm.Current} Downed层数={targetTags.Stack(CommonTags.Downed)}",
                () => DemoTrace.Snapshot(target));
            world.Deliver(new IEffect[] { new HitStunEffect { Duration = 1f } }, attacker, target, 10f);
            trace.Check("HitStun 不能覆盖 Knockdown", targetFsm.Current == ActivityId.Knockdown && !targetTags.Has(CommonTags.Stunned),
                "Activity=Knockdown 且 Stunned=false", $"Activity={targetFsm.Current} Stunned={targetTags.Has(CommonTags.Stunned)}",
                () => DemoTrace.Snapshot(target));
            trace.AdvanceUntil("等待 Knockdown 恢复 Root", () => targetFsm.Current == ActivityId.Root, 0.05f, 20,
                () => DemoTrace.Snapshot(target));
            trace.Check("恢复后清理 Downed Tag", targetFsm.Current == ActivityId.Root && !targetTags.Has(CommonTags.Downed),
                "Activity=Root 且 Downed=false", $"Activity={targetFsm.Current} Downed={targetTags.Has(CommonTags.Downed)}",
                () => DemoTrace.Snapshot(target));

            // Knockdown 可以覆盖已有的 Hit 姿态；倒地期间只由 Downed 姿态持有状态 Tag。
            world.Deliver(new IEffect[] { new HitStunEffect { Duration = 1f } }, attacker, target, 10f);
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, attacker, target, 10f);
            trace.Check("Knockdown 覆盖已有 Hit", targetFsm.Current == ActivityId.Knockdown && !targetTags.Has(CommonTags.Stunned),
                "Activity=Knockdown 且 Stunned=false", $"Activity={targetFsm.Current} Stunned={targetTags.Has(CommonTags.Stunned)}",
                () => DemoTrace.Snapshot(target));
            trace.AdvanceFor("推进第二次倒地计时", 0.05f, 16, () => DemoTrace.Snapshot(target));

            // 玩家倒地时清空待处理输入并停止活动 Timeline；随后 Dead 仍然拥有最高优先级。
            var fsm = attacker.GetComp<StateMachineComp>();
            var tags = attacker.GetComp<TagComp>();
            var input = attacker.GetComp<InputBufferComp>();
            input.Push(InputToken.Attack);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            trace.Check("倒地前技能已启动", attacker.GetComp<SkillDirectorComp>().IsPlaying, "技能播放中", $"playing={attacker.GetComp<SkillDirectorComp>().IsPlaying}",
                () => DemoTrace.Snapshot(attacker));
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.8f } }, target, attacker, 10f);
            bool playerAttack = fsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "DownedAttack" });
            trace.Check("玩家倒地停止技能并清空输入", fsm.Current == ActivityId.Knockdown && tags.Has(CommonTags.Downed) &&
                !attacker.GetComp<SkillDirectorComp>().IsPlaying && !input.HasBuffered && !playerAttack,
                "倒地、技能停止、输入为空、Attack被拒绝",
                $"Activity={fsm.Current} Downed={tags.Has(CommonTags.Downed)} playing={attacker.GetComp<SkillDirectorComp>().IsPlaying} buffered={input.HasBuffered} TryAttack={playerAttack}",
                () => DemoTrace.Snapshot(attacker));
            trace.AdvanceFor("推进玩家倒地计时", 0.05f, 16, () => DemoTrace.Snapshot(attacker));
            world.Deliver(new IEffect[] { new KnockdownEffect() }, target, attacker, 10f);
            fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "DeadWins" });
            trace.Check("Dead 覆盖 Knockdown 并成为终态", fsm.Current == ActivityId.Dead, "Activity=Dead", $"Activity={fsm.Current}",
                () => DemoTrace.Snapshot(attacker));
            trace.Complete("倒地优先级、刷新、输入门控与死亡终态验证完成");
        }
    }
}
