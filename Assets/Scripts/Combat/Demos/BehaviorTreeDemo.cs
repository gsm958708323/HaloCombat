using Combat.Core;

namespace Combat.Demos
{
    // 行为树用例：验证 AI 自主选敌、技能播放、倒地停机以及共享伤害管线。
    public static class BehaviorTreeDemo
    {
        public static void Run()
        {
            // 记录 AI 造成的伤害和目标无敌事件，验证行为树只负责编排，不直接结算伤害。
            var events = new EventBus();
            int aiHits = 0, immune = 0;
            events.Subscribe<EvDamage>(e => { if (e.Source.IsValid) aiHits++; });
            events.Subscribe<EvImmune>(_ => immune++);
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai", 2.5f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var secondEnemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 4f, 0f);
            var enemyBt = enemy.GetComp<BehaviorTreeComp>();
            var enemyDirector = enemy.GetComp<SkillDirectorComp>();
            // AI Actor 不应拥有玩家输入路径；每个 Actor 还必须使用独立的行为树黑板。
            SeasonTwoDemoSupport.Assert(enemyBt.Board.Target.IsValid == false, "bt target starts empty");
            SeasonTwoDemoSupport.Assert(!enemy.TryGetComp<InputBufferComp>(out _) && !enemy.TryGetComp<ComboComp>(out _), "ai has no player input path");
            SeasonTwoDemoSupport.Assert(!ReferenceEquals(enemyBt.Board, secondEnemy.GetComp<BehaviorTreeComp>().Board), "tree board cloned per actor");
            bool sawG1 = false;
            // 推进逻辑时间，让 Perception 写入目标后由行为树自行接近并播放 G1。
            for (int i = 0; i < 90; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                if (enemyDirector.CurrentSkill == SkillNodeId.G1)
                    sawG1 = true;
            }
            SeasonTwoDemoSupport.Assert(sawG1, "bt plays G1");
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f && aiHits > 0, "bt play skill");
            // 进入 Knockdown 后，行为树和当前技能都必须停止，倒地期间不能继续攻击。
            enemy.GetComp<StateMachineComp>().TryEnter(ActivityId.Knockdown, new ActivityEnterArgs { Reason = "DemoDown" });
            SeasonTwoDemoSupport.Assert(enemy.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                !enemyDirector.IsPlaying, "downed stops active bt skill");
            float hp = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hp, "downed stops bt");

            for (int i = 0; i < 25; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            // 恢复玩家后启动闪避，再用同一个伤害包确认无敌判断仍走中央管线。
            var playerTags = stake.GetComp<TagComp>();
            stake.GetComp<InputBufferComp>().Push(Season2Tokens.Dodge);
            SeasonTwoDemoSupport.Step(world, 0.06f);
            SeasonTwoDemoSupport.Assert(playerTags.Has(CommonTags.Invincible), "bt pipeline iframe");
            float hpI = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(TimelineSO.G1Melee.Bake(), enemy, stake, 10f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hpI && immune > 0,
                "bt damage uses central iframe pipeline");
            CombatLog.Info(CombatCategories.BehaviorTree, "BehaviorTreeDemo PASSED");
        }
    }
}
