using System;
using Combat.Core;

namespace Combat.Demos
{
    // 光环与追踪弹用例：验证 AoE Occupancy 的独立来源、离开清理以及 Homing 行为。
    public static class AuraHomingDemo
    {
        public static void Run()
        {
            // 子场景一使用独立世界，确保光环 Buff 不会影响后面的投射物测试。
            var auraWorld = SeasonTwoDemoSupport.NewWorld();
            var auraOwner = SeasonTwoDemoSupport.Spawn(auraWorld, "fighter", 0f, 0f);
            var auraTarget = SeasonTwoDemoSupport.Spawn(auraWorld, "stake", 0.4f, 0f);
            var auraAttr = auraTarget.GetComp<AttributeSet>();
            var auraBuff = auraTarget.GetComp<BuffComp>();
            float baseSpeed = auraAttr.GetFinal(AttrId.MoveSpeed);
            var auraTrace = new DemoTrace("AuraHoming", CombatCategories.AuraHoming, auraWorld, dt => SeasonTwoDemoSupport.Step(auraWorld, dt));
            auraTrace.Step("初始化 Aura Occupancy 子场景", () => DemoTrace.Snapshot(auraTarget));
            auraWorld.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, auraOwner, null, 0f,
                auraOwner.GetComp<TransformComp>().Position);
            auraTrace.AdvanceFor("第一来源进入目标范围", 0.05f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            auraTrace.Check("第一来源进入后产生一层减速", auraBuff.StacksOf(CombatIds.AuraSlow) == 1 &&
                Math.Abs(auraAttr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.5f) < 1e-3f,
                "层数=1 且速度=基础值*0.5", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(auraTarget));

            // 两个不同拥有者的光环各自贡献一层减速，不能合并成一个不可区分的状态。
            var auraOwnerTwo = SeasonTwoDemoSupport.Spawn(auraWorld, "fighter", 0.8f, 0f);
            auraWorld.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, auraOwnerTwo, null, 0f,
                auraOwnerTwo.GetComp<TransformComp>().Position);
            auraTrace.AdvanceFor("第二来源进入并叠加 Occupancy", 0.05f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            auraTrace.Check("双来源分别占用并叠加两层减速", auraBuff.StacksOf(CombatIds.AuraSlow) == 2 &&
                Math.Abs(auraAttr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.25f) < 1e-3f,
                "层数=2 且速度=基础值*0.25", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(auraTarget));

            auraTarget.GetComp<TransformComp>().Position = new SimVec3(1.8f, 0f, 0f);
            auraTrace.AdvanceFor("目标离开一个光环范围", 0.05f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            auraTrace.Check("离开一个光环后保留另一层减速", auraBuff.StacksOf(CombatIds.AuraSlow) == 1 &&
                Math.Abs(auraAttr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.5f) < 1e-3f,
                "剩余层数=1 且速度恢复到0.5倍", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(auraTarget));
            auraTarget.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            auraTrace.AdvanceFor("目标离开全部光环范围", 0.05f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            auraTrace.Check("离开全部光环后清理减速", auraBuff.StacksOf(CombatIds.AuraSlow) == 0 &&
                Math.Abs(auraAttr.GetFinal(AttrId.MoveSpeed) - baseSpeed) < 1e-3f,
                "层数=0 且速度恢复基础值", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(auraTarget));

            // Occupancy 来源彼此独立：移除任一拥有者的 AoE 只能移除对应的一层。
            auraTarget.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            auraTrace.AdvanceFor("目标重新进入两个光环", 0.05f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)}");
            auraTrace.Check("目标重新进入后恢复两层 Occupancy", auraBuff.StacksOf(CombatIds.AuraSlow) == 2, "层数=2", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)}",
                () => DemoTrace.Snapshot(auraTarget));
            auraOwner.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "AuraOwnerDead" });
            auraTrace.AdvanceFor("第一个 Owner 死亡并清理自身光环", 0.02f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)}");
            auraTrace.Check("单个 Owner 死亡只清理自身光环", auraBuff.StacksOf(CombatIds.AuraSlow) == 1, "层数=1", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)}",
                () => DemoTrace.Snapshot(auraTarget));
            auraOwnerTwo.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "AuraOwnerTwoDead" });
            auraTrace.AdvanceFor("全部 Owner 死亡并清理光环", 0.02f, 1,
                () => $"AuraSlow层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            auraTrace.Check("全部 Owner 死亡后清理剩余光环", auraBuff.StacksOf(CombatIds.AuraSlow) == 0 &&
                Math.Abs(auraAttr.GetFinal(AttrId.MoveSpeed) - baseSpeed) < 1e-3f,
                "层数=0 且速度恢复基础值", $"层数={auraBuff.StacksOf(CombatIds.AuraSlow)} speed={auraAttr.GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(auraTarget));
            auraTrace.Complete("Aura Occupancy 子场景完成");

            // 子场景二使用另一份世界，专门比较 Homing 与直线 Fireball。
            var homingWorld = SeasonTwoDemoSupport.NewWorld();
            var shooter = SeasonTwoDemoSupport.Spawn(homingWorld, "fighter", -3f, 0f);
            var second = SeasonTwoDemoSupport.Spawn(homingWorld, "stake", 1.5f, 2f);
            var homingTrace = new DemoTrace("AuraHoming", CombatCategories.AuraHoming, homingWorld, dt => SeasonTwoDemoSupport.Step(homingWorld, dt));
            homingTrace.Step("初始化 Homing Projectile 子场景", () => $"{DemoTrace.Snapshot(shooter)} target={DemoTrace.Snapshot(second)}");
            shooter.GetComp<TransformComp>().YawDegrees = 0f;
            float homingHp = second.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            homingWorld.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, shooter, second, 10f);
            homingTrace.AdvanceUntil("追踪弹转向并命中偏置目标", () => second.GetComp<AttributeSet>().GetBase(AttrId.Hp) < homingHp,
                0.05f, 25, () => $"targetHp={second.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} {DemoTrace.Snapshot(second)}");
            homingTrace.Check("Homing 转向后命中偏置目标", second.GetComp<AttributeSet>().GetBase(AttrId.Hp) < homingHp,
                "目标HP下降", $"初始HP={homingHp.ToString("F1")} 当前HP={second.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(second));
            var straightTarget = SeasonTwoDemoSupport.Spawn(homingWorld, "stake", 3f, 2.2f);
            float straightHp = straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            homingWorld.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) }, shooter, null, 10f);
            homingTrace.AdvanceFor("推进直线 Fireball 飞行", 0.02f, 40,
                () => $"偏置目标HP={straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}");
            homingTrace.Check("直线 Fireball 无法命中偏置目标", straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp) == straightHp,
                "偏置目标HP不变", $"初始HP={straightHp.ToString("F1")} 当前HP={straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(straightTarget));
            homingTrace.Complete("Homing 命中与直线投射物对比完成");
        }
    }
}
