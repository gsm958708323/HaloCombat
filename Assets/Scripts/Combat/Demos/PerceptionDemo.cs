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
            var perception = enemy.GetComp<PerceptionComp>();
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!perception.Forced.IsValid, "outside alert");
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 1f } }, player, enemy, 10f);
            SeasonTwoDemoSupport.Assert(perception.Forced == player.Id, "hurt forces target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");
            player.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!enemy.GetComp<BehaviorTreeComp>().Board.Target.IsValid, "perception loses target");
            player.GetComp<TransformComp>().Position = new SimVec3(1f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Target == player.Id, "perception writes board");
            CombatLog.Info(CombatCategories.Perception, "PerceptionDemo PASSED");
        }
    }
}
