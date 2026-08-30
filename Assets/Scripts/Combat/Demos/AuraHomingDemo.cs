using System;
using Combat.Core;

namespace Combat.Demos
{
    // 光环与追踪弹用例：验证 AoE Occupancy 的独立来源、离开清理以及 Homing 行为。
    public static class AuraHomingDemo
    {
        public static void Run()
        {
            // 第一个 AuraField 进入目标后，应添加减速 Buff 并把速度乘以 0.5。
            var world = SeasonTwoDemoSupport.NewWorld();
            var owner = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.4f, 0f);
            var attr = target.GetComp<AttributeSet>();
            float baseSpeed = attr.GetFinal(AttrId.MoveSpeed);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, owner, null, 0f, owner.GetComp<TransformComp>().Position);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 1 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.5f) < 1e-3f, "aura enter slow");
            // 两个不同拥有者的光环各自贡献一层减速，不能合并成一个不可区分的状态。
            var ownerTwo = SeasonTwoDemoSupport.Spawn(world, "fighter", 0.8f, 0f);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, ownerTwo, null, 0f, ownerTwo.GetComp<TransformComp>().Position);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 2 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.25f) < 1e-3f, "two aura sources stack");
            target.GetComp<TransformComp>().Position = new SimVec3(1.8f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 1 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.5f) < 1e-3f, "one aura exits");
            target.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 0 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed) < 1e-3f, "aura exit restore");

            // Occupancy 来源彼此独立：移除任一拥有者的 AoE 只能移除对应的一层，
            // 两个拥有者都死亡后才恢复原始速度。
            target.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 2, "aura reenter");
            owner.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "AuraOwnerDead" });
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 1, "first aura owner cleanup");
            ownerTwo.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "AuraOwnerTwoDead" });
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 0 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed) < 1e-3f, "all aura owner cleanup");

            var shooter = SeasonTwoDemoSupport.Spawn(world, "fighter", -3f, 0f);
            var second = SeasonTwoDemoSupport.Spawn(world, "stake", 1.5f, 2f);
            // HomingBolt 锁定偏移目标后会转向命中；Fireball 没有追踪能力，只能沿直线飞行。
            shooter.GetComp<TransformComp>().YawDegrees = 0f;
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, shooter, second, 10f);
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(second.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "homing hit");

            var straightTarget = SeasonTwoDemoSupport.Spawn(world, "stake", 3f, 2.2f);
            float straightHp = straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) }, shooter, null, 10f);
            for (int i = 0; i < 40; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(straightTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp) == straightHp,
                "straight projectile misses offset target");
            CombatLog.Info(CombatCategories.AuraHoming, "AuraHomingDemo PASSED");
        }
    }
}
