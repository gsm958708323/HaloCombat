using Combat.Core;

namespace Combat.Demos
{
    // 感知用例：验证警戒半径、受伤强制选敌、目标丢失和多个敌人的独立黑板。
    public static class PerceptionDemo
    {
        public static void Run()
        {
            // narrow 敌人的感知范围较小，远处玩家和旁观敌人都不应立即被发现。
            var events = new EventBus();
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 0f, 0f);
            var player = SeasonTwoDemoSupport.Spawn(world, "fighter", 10f, 0f);
            var bystander = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 6f, 5f);
            var perception = enemy.GetComp<PerceptionComp>();
            var board = enemy.GetComp<BehaviorTreeComp>().Board;
            var trace = new DemoTrace("Perception", CombatCategories.Perception, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("init", "初始化感知范围、Forced 与 Blackboard", () => DemoTrace.Snapshot(enemy) + " player=" + DemoTrace.Snapshot(player));
            trace.AdvanceFor("out-of-range", "范围外目标不触发感知", 0.02f, 1,
                () => "Forced=" + perception.Forced + " Board.Target=" + board.Target);
            trace.Check("out-of-range-result", "范围外不设置 Forced 且敌人 Blackboard 保持无目标", !perception.Forced.IsValid &&
                !board.Target.IsValid && !bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid,
                "Forced为空、主敌人 Target为空且旁观者 Target为空",
                "Forced=" + perception.Forced + " 主敌人Target=" + board.Target + " 旁观者Target=" + bystander.GetComp<BehaviorTreeComp>().Board.Target,
                () => DemoTrace.Snapshot(enemy));
            // 受到玩家伤害后，Perception 记录伤害来源，即使该来源原本在警戒范围外。
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 1f } }, player, enemy, 10f);
            trace.Check("forced-by-hit", "受到伤害后记录强制目标", perception.Forced == player.Id,
                "Forced=player", "Forced=" + perception.Forced, () => DemoTrace.Snapshot(enemy));
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            // 目标进入范围后，感知组件把目标写入自己的行为树黑板。
            trace.AdvanceUntil("enter-range", "目标进入范围并写入 Blackboard", () => board.Target == player.Id, 0.02f, 3,
                () => "Forced=" + perception.Forced + " Board.Target=" + board.Target);
            trace.Check("enter-range-result", "进入范围只更新主敌人的 Blackboard", board.Target == player.Id && !bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid,
                "主敌人 Target=player 且旁观者 Target为空", "主敌人 Target=" + board.Target + " 旁观者 Target=" + bystander.GetComp<BehaviorTreeComp>().Board.Target,
                () => DemoTrace.Snapshot(enemy));
            player.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            // 目标离开范围后清空黑板；重新进入范围则再次获得目标。
            trace.AdvanceUntil("leave-range", "目标离开范围并清空 Blackboard", () => !board.Target.IsValid, 0.02f, 3,
                () => "Forced=" + perception.Forced + " Board.Target=" + board.Target);
            trace.Check("leave-range-result", "离开范围后清空 Blackboard 目标", !board.Target.IsValid,
                "Board.Target为空", "Board.Target=" + board.Target, () => DemoTrace.Snapshot(enemy));
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            trace.AdvanceUntil("reenter-range", "目标重新进入范围并再次获得", () => board.Target == player.Id, 0.02f, 3,
                () => "Forced=" + perception.Forced + " Board.Target=" + board.Target);
            trace.Check("reenter-range-result", "重新进入范围后再次获得目标", board.Target == player.Id,
                "Board.Target=player", "Board.Target=" + board.Target, () => DemoTrace.Snapshot(enemy));

            var first = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, -2f);
            var second = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, 2f);
            player.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            // 两个敌人共享同一个玩家目标，但各自维护自己的 BehaviorTree Board。
            trace.AdvanceUntil("multi-acquire", "多个敌人独立获取同一目标", () =>
                first.GetComp<BehaviorTreeComp>().Board.Target == player.Id && second.GetComp<BehaviorTreeComp>().Board.Target == player.Id,
                0.05f, 30, () => "first=" + first.GetComp<BehaviorTreeComp>().Board.Target + " second=" + second.GetComp<BehaviorTreeComp>().Board.Target);
            trace.Check("multi-acquire-result", "多个敌人分别写入独立 Blackboard", first.GetComp<BehaviorTreeComp>().Board.Target == player.Id &&
                second.GetComp<BehaviorTreeComp>().Board.Target == player.Id &&
                !ReferenceEquals(first.GetComp<BehaviorTreeComp>().Board, second.GetComp<BehaviorTreeComp>().Board),
                "两个 Board.Target=player 且 Blackboard 独立",
                "first=" + first.GetComp<BehaviorTreeComp>().Board.Target + " second=" + second.GetComp<BehaviorTreeComp>().Board.Target,
                () => DemoTrace.Snapshot(first));
            trace.Complete("范围、强制目标、进入离开与多敌人感知验证完成");
        }
    }
}
