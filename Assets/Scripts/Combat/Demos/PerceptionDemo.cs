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
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!perception.Forced.IsValid, "outside alert");
            SeasonTwoDemoSupport.Assert(!bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "bystander stays idle");
            // 受到玩家伤害后，Perception 记录伤害来源，即使该来源原本在警戒范围外。
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 1f } }, player, enemy, 10f);
            SeasonTwoDemoSupport.Assert(perception.Forced == player.Id, "hurt forces target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            // 目标进入范围后，感知组件把目标写入自己的行为树黑板。
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");
            SeasonTwoDemoSupport.Assert(!bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "bystander not alerted");
            player.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            // 目标离开范围后清空黑板；重新进入范围则再次获得目标。
            SeasonTwoDemoSupport.Assert(!enemy.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "perception loses target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");

            var first = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, -2f);
            var second = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, 2f);
            player.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            // 两个敌人共享同一个玩家目标，但各自维护自己的 BehaviorTree Board。
            for (int i = 0; i < 30; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(first.GetComp<BehaviorTreeComp>().Board.Target == player.Id &&
                second.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "multiple enemies acquire independently");
            CombatLog.Info(CombatCategories.Perception, "PerceptionDemo PASSED");
        }
    }
}
