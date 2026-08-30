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
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai", 2.5f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var secondEnemy = SeasonTwoDemoSupport.Spawn(world, "melee_ai_narrow", 4f, 0f);
            EntityId enemyId = enemy.Id;
            EntityId stakeId = stake.Id;
            int aiHits = 0;
            events.Subscribe<EvDamage>(e =>
            {
                if (e.Source == enemyId && e.Target == stakeId)
                    aiHits++;
            });
            var enemyBt = enemy.GetComp<BehaviorTreeComp>();
            var enemyDirector = enemy.GetComp<SkillDirectorComp>();
            var trace = new DemoTrace("BehaviorTree", CombatCategories.BehaviorTree, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("init", "初始化 AI Actor 与独立 Blackboard", () => $"{DemoTrace.Snapshot(enemy)} target={DemoTrace.Snapshot(stake)}");
            // AI Actor 不应拥有玩家输入路径；每个 Actor 还必须使用独立的行为树黑板。
            trace.Check("board-init", "初始 Blackboard 没有目标且 Actor 无玩家输入路径", !enemyBt.Board.Target.IsValid &&
                !enemy.TryGetComp<InputBufferComp>(out _) && !enemy.TryGetComp<ComboComp>(out _) &&
                !ReferenceEquals(enemyBt.Board, secondEnemy.GetComp<BehaviorTreeComp>().Board),
                "Target为空、无 Input/Combo、每个 Actor Blackboard 独立",
                $"Target={enemyBt.Board.Target} input={enemy.TryGetComp<InputBufferComp>(out _)} combo={enemy.TryGetComp<ComboComp>(out _)}",
                () => DemoTrace.Snapshot(enemy));
            trace.AdvanceUntil("perception-target", "Perception 写入行为树目标", () => enemyBt.Board.Target == stake.Id,
                0.02f, 10, () => $"Board.Target={enemyBt.Board.Target} {DemoTrace.Snapshot(enemy)}");
            trace.Check("board-target", "Blackboard 保存感知目标", enemyBt.Board.Target == stake.Id,
                "Board.Target=目标桩", $"Board.Target={enemyBt.Board.Target}", () => DemoTrace.Snapshot(enemy));
            bool sawG1 = false;
            // 推进逻辑时间，让 Perception 写入目标后由行为树自行接近并播放 G1。
            trace.AdvanceUntil("bt-play-g1", "行为树播放 G1 并通过中央管线造成伤害", () =>
            {
                if (enemyDirector.CurrentSkill == SkillNodeId.G1)
                    sawG1 = true;
                return sawG1 && aiHits > 0;
            }, 0.02f, 90, () => $"sawG1={sawG1} aiHits={aiHits} targetHp={stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}");
            trace.Check("bt-play-result", "行为树播放 G1 且目标HP下降", sawG1 && stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f && aiHits > 0,
                "播放 G1 且目标HP下降", $"sawG1={sawG1} aiHits={aiHits} targetHp={stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(stake));
            // 进入 Knockdown 后，行为树和当前技能都必须停止，倒地期间不能继续攻击。
            enemy.GetComp<StateMachineComp>().TryEnter(ActivityId.Knockdown, new ActivityEnterArgs { Reason = "DemoDown" });
            trace.Check("knockdown-stop", "Knockdown 停止行为树正在播放的技能", enemy.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                !enemyDirector.IsPlaying, "Activity=Knockdown 且技能停止", $"Activity={enemy.GetComp<StateMachineComp>().Current} playing={enemyDirector.IsPlaying}",
                () => DemoTrace.Snapshot(enemy));
            float hp = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            trace.AdvanceFor("knockdown-window", "倒地期间推进并观察行为树停机", 0.02f, 20,
                () => $"targetHp={stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} {DemoTrace.Snapshot(enemy)}");
            trace.Check("knockdown-result", "倒地期间目标HP保持不变", stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hp,
                "倒地期间目标HP不再下降", $"此前HP={hp.ToString("F1")} 当前HP={stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(enemy));
            trace.Complete("Blackboard、AI 技能编排与倒地停机验证完成");
        }
    }
}
