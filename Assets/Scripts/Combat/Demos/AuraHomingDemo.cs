using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class AuraHomingDemo
    {
        public static void Run()
        {
            var world = SeasonTwoDemoSupport.NewWorld();
            var owner = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.4f, 0f);
            var attr = target.GetComp<AttributeSet>();
            float baseSpeed = attr.GetFinal(AttrId.MoveSpeed);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, owner, null, 0f, owner.GetComp<TransformComp>().Position);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 1 &&
                Math.Abs(attr.GetFinal(AttrId.MoveSpeed) - baseSpeed * 0.5f) < 1e-3f, "aura enter slow");
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

            // Each occupancy source is independent. Removing either owner's AoE
            // must remove only that source's stack, and removing both restores the
            // original speed.
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
