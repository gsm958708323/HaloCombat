using System;
using Combat.Core;

namespace Combat.Demos
{
    // 召唤用例：验证召唤物的拥有者关系、跟随、独立 AI 攻击和级联清理。
    public static class SummonDemo
    {
        public static void Run()
        {
            // 通过伤害事件记录真正的 Source，确保伤害归属召唤物而不是玩家。
            var events = new EventBus();
            EntityId summonSource = EntityId.Invalid;
            bool summonHit = false;
            events.Subscribe<EvDamage>(e => { if (summonSource.IsValid && e.Source == summonSource) summonHit = true; });
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var owner = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "stake", 10f, 0f);
            // SpawnSummonEffect 创建的是没有玩家输入组件、但拥有自己 BT/Timeline 的 Actor。
            world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, owner, null, 0f, owner.GetComp<TransformComp>().Position);
            Actor summon = null;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++) if (actors[i].TryGetComp<SummonComp>(out _)) summon = actors[i];
            SeasonTwoDemoSupport.Assert(summon != null, "summon spawn");
            SeasonTwoDemoSupport.Assert(summon.GetComp<SummonComp>().OwnerId == owner.Id, "summon owner");
            SeasonTwoDemoSupport.Assert(summon.GetComp<BehaviorTreeComp>().Board.Owner == owner.Id, "owner reaches bt board");
            SeasonTwoDemoSupport.Assert(!summon.TryGetComp<InputBufferComp>(out _) && !summon.TryGetComp<ComboComp>(out _), "summon has no player input path");
            summonSource = summon.Id;
            // 先给宠物和主人一段跟随时间，再把目标移到近战范围内验证攻击。
            for (int i = 0; i < 40; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            var petTf = summon.GetComp<TransformComp>();
            var ownerTf = owner.GetComp<TransformComp>();
            float followDx = petTf.Position.X - ownerTf.Position.X;
            float followDz = petTf.Position.Z - ownerTf.Position.Z;
            SeasonTwoDemoSupport.Assert(Math.Sqrt(followDx * followDx + followDz * followDz) <= 2.2f, "summon follows owner");

            stake.GetComp<TransformComp>().Position = new SimVec3(1.2f, 0f, 0f);
            float hp0 = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            for (int i = 0; i < 80; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                if (stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hp0 - 0.1f)
                    break;
            }
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "summon bt attack");
            SeasonTwoDemoSupport.Assert(summonHit, "summon is damage source");
            float hp1 = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            // 主人死亡会清理召唤物及其后代运行时对象，目标木桩必须继续存在。
            owner.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "OwnerDead" });
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!summon.IsActive && !world.TryGetActor(summon.Id, out _) && stake.IsActive, "owner cleanup summon");
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hp1, "summon stops after owner cleanup");
            CombatLog.Info(CombatCategories.Summon, "SummonDemo PASSED");
        }
    }
}
