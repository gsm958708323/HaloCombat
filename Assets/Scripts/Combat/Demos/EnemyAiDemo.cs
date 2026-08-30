using Combat.Core;

namespace Combat.Demos
{
    public static class EnemyAiDemo
    {
        public static void Run()
        {
            var world = SeasonTwoDemoSupport.NewWorld();
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_guard", 0f, 0f);
            var player = SeasonTwoDemoSupport.Spawn(world, "fighter", 2f, 0f);
            for (int i = 0; i < 100; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(player.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "enemy attacks");
            SeasonTwoDemoSupport.Assert(enemy.GetComp<BehaviorTreeComp>().Board.Home.X == 0f, "enemy records home");
            player.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            for (int i = 0; i < 150; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<TransformComp>().Position.X < 3f, "enemy leash");
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, player, enemy, 10f);
            var stoppedAt = enemy.GetComp<TransformComp>().Position.X;
            for (int i = 0; i < 5; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                enemy.GetComp<TransformComp>().Position.X == stoppedAt, "downed stops enemy");
            CombatLog.Info(CombatCategories.EnemyAi, "EnemyAiDemo PASSED");
        }
    }
}
