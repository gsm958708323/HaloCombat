using Combat.Core;

namespace Combat.Demos
{
    public static class BehaviorTreeDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            int aiHits = 0;
            events.Subscribe<EvDamage>(e => { if (e.Source.IsValid) aiHits++; });
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai", 2.5f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var secondEnemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 4f, 0f);
            var enemyBt = enemy.GetComp<BehaviorTreeComp>();
            SeasonTwoDemoSupport.Assert(enemyBt.Board.Target.IsValid == false, "bt target starts empty");
            SeasonTwoDemoSupport.Assert(!enemy.TryGetComp<InputBufferComp>(out _) && !enemy.TryGetComp<ComboComp>(out _), "ai has no player input path");
            SeasonTwoDemoSupport.Assert(!ReferenceEquals(enemyBt.Board, secondEnemy.GetComp<BehaviorTreeComp>().Board), "tree board cloned per actor");
            for (int i = 0; i < 90; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f && aiHits > 0, "bt play skill");
            enemy.GetComp<StateMachineComp>().TryEnter(ActivityId.Knockdown, new ActivityEnterArgs { Reason = "DemoDown" });
            float hp = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hp, "downed stops bt");
            CombatLog.Info(CombatCategories.BehaviorTree, "BehaviorTreeDemo PASSED");
        }
    }
}
