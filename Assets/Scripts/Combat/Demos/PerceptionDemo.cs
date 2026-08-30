using Combat.Core;

namespace Combat.Demos
{
    public static class PerceptionDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 0f, 0f);
            var player = SeasonTwoDemoSupport.Spawn(world, "fighter", 10f, 0f);
            var bystander = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 6f, 5f);
            var perception = enemy.GetComp<PerceptionComp>();
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!perception.Forced.IsValid, "outside alert");
            SeasonTwoDemoSupport.Assert(!bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "bystander stays idle");
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 1f } }, player, enemy, 10f);
            SeasonTwoDemoSupport.Assert(perception.Forced == player.Id, "hurt forces target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");
            SeasonTwoDemoSupport.Assert(!bystander.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "bystander not alerted");
            player.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!enemy.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "perception loses target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");

            var first = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, -2f);
            var second = SeasonTwoDemoSupport.Spawn(world, "melee_ai", -2f, 2f);
            player.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            for (int i = 0; i < 30; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(first.GetComp<BehaviorTreeComp>().Board.Target == player.Id &&
                second.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "multiple enemies acquire independently");
            CombatLog.Info(CombatCategories.Perception, "PerceptionDemo PASSED");
        }
    }
}
