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
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var owner = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "stake", 10f, 0f);
            events.Subscribe<EvDamage>(e =>
            {
                // 伤害 Source 必须是召唤物本身，且目标限定为本 Demo 的目标桩。
                if (summonSource.IsValid && e.Source == summonSource && e.Target == stake.Id)
                    summonHit = true;
            });
            var trace = new DemoTrace("Summon", CombatCategories.Summon, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("init", "初始化 Owner、目标与召唤目录", () => DemoTrace.Snapshot(owner) + " target=" + DemoTrace.Snapshot(stake));
            // SpawnSummonEffect 创建的是没有玩家输入组件、但拥有自己 BT/Timeline 的 Actor。
            world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, owner, null, 0f, owner.GetComp<TransformComp>().Position);
            Actor summon = null;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++) if (actors[i].TryGetComp<SummonComp>(out _)) summon = actors[i];
            trace.Check("spawn", "创建召唤物并建立 Owner 关系", summon != null, "召唤物存在", "召唤物存在=" + (summon != null),
                () => summon == null ? "无 summon" : DemoTrace.Snapshot(summon));
            trace.Check("ownership", "Owner 关系写入 Summon 与 Blackboard", summon != null && summon.GetComp<SummonComp>().OwnerId == owner.Id &&
                summon.GetComp<BehaviorTreeComp>().Board.Owner == owner.Id &&
                !summon.TryGetComp<InputBufferComp>(out _) && !summon.TryGetComp<ComboComp>(out _),
                "OwnerId正确、Board.Owner正确、无玩家输入组件",
                summon == null ? "无 summon" : "OwnerId=" + summon.GetComp<SummonComp>().OwnerId + " Board.Owner=" + summon.GetComp<BehaviorTreeComp>().Board.Owner,
                () => summon == null ? "无 summon" : "OwnerId=" + summon.GetComp<SummonComp>().OwnerId + " Board.Owner=" + summon.GetComp<BehaviorTreeComp>().Board.Owner + " " + DemoTrace.Snapshot(summon));
            summonSource = summon.Id;
            // 先给宠物和主人一段跟随时间，再把目标移到近战范围内验证攻击。
            trace.AdvanceFor("follow", "召唤物跟随 Owner", 0.05f, 40, () => DemoTrace.Snapshot(summon));
            var petTf = summon.GetComp<TransformComp>();
            var ownerTf = owner.GetComp<TransformComp>();
            float followDx = petTf.Position.X - ownerTf.Position.X;
            float followDz = petTf.Position.Z - ownerTf.Position.Z;
            trace.Check("follow-result", "召唤物跟随后保持在 Owner 附近", Math.Sqrt(followDx * followDx + followDz * followDz) <= 2.2f,
                "与 Owner 距离<=2.2", "距离=" + Math.Sqrt(followDx * followDx + followDz * followDz).ToString("F2"), () => DemoTrace.Snapshot(summon));

            stake.GetComp<TransformComp>().Position = new SimVec3(1.2f, 0f, 0f);
            float hp0 = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            trace.AdvanceUntil("attack", "召唤物通过自己的 BT/Timeline 攻击", () => stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hp0 - 0.1f,
                0.02f, 80, () => "targetHp=" + stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1") + " summonHit=" + summonHit + " " + DemoTrace.Snapshot(summon));
            trace.Check("attack-result", "召唤物攻击造成伤害且 Source 正确", stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f && summonHit,
                "目标HP下降且 Source=召唤物", "targetHp=" + stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1") + " summonHit=" + summonHit,
                () => "source=" + summonSource + " target=" + stake.Id + " " + DemoTrace.Snapshot(summon));
            float hp1 = stake.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            // 手动留下两个后代对象，观察 Owner 清理是否级联到 Projectile/AoE。
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball), new SpawnAoeEffect(CombatIds.FireGround) }, summon, null,
                summon.GetComp<AttributeSet>().GetFinal(AttrId.Atk), summon.GetComp<TransformComp>().Position);
            trace.AdvanceFor("descendant-prepare", "准备召唤物的 Projectile 与 AoE 后代", 0.02f, 1,
                () => "descendants=" + CountOwnedRuntime(world, summon.Id) + " " + DemoTrace.Snapshot(summon));
            // 主人死亡会清理召唤物及其后代运行时对象，目标木桩必须继续存在。
            owner.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "OwnerDead" });
            trace.AdvanceFor("owner-cleanup", "Owner 死亡并级联清理召唤物及后代", 0.02f, 1,
                () => "summonActive=" + summon.IsActive + " descendants=" + CountOwnedRuntime(world, summon.Id));
            trace.Check("owner-cleanup-result", "Owner 死亡后清理召唤物及其后代", !summon.IsActive && !world.TryGetActor(summon.Id, out _) &&
                CountOwnedRuntime(world, summon.Id) == 0 && stake.IsActive,
                "召唤物和后代均清理、目标仍存在", "summonActive=" + summon.IsActive + " descendants=" + CountOwnedRuntime(world, summon.Id) + " targetActive=" + stake.IsActive,
                () => DemoTrace.Snapshot(owner));
            trace.AdvanceFor("post-cleanup", "清理后继续推进世界", 0.05f, 20,
                () => "targetHp=" + stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1"));
            trace.Check("post-cleanup-result", "清理后目标不再受到召唤物伤害", stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hp1,
                "目标HP保持清理前数值", "此前HP=" + hp1.ToString("F1") + " 当前HP=" + stake.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1"),
                () => DemoTrace.Snapshot(stake));
            trace.Complete("Owner 关系、跟随、召唤物攻击与级联清理验证完成");
        }

        static int CountOwnedRuntime(CombatWorld world, EntityId ownerId)
        {
            int count = 0;
            var active = world.RegistryActive();
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].TryGetComp<ProjectileComp>(out var p) && p.OwnerId == ownerId) count++;
                if (active[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == ownerId) count++;
            }
            return count;
        }
    }
}
