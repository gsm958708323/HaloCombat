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
            SeasonTwoDemoSupport.Assert(attr.GetFinal(AttrId.MoveSpeed) < baseSpeed, "aura enter slow");
            var ownerTwo = SeasonTwoDemoSupport.Spawn(world, "fighter", 0.8f, 0f);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, ownerTwo, null, 0f, ownerTwo.GetComp<TransformComp>().Position);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().Count == 2, "two aura sources stack");
            target.GetComp<TransformComp>().Position = new SimVec3(1.8f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(target.GetComp<BuffComp>().Count == 1 && attr.GetFinal(AttrId.MoveSpeed) < baseSpeed, "one aura exits");
            target.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(attr.GetFinal(AttrId.MoveSpeed) >= baseSpeed, "aura exit restore");

            var shooter = SeasonTwoDemoSupport.Spawn(world, "fighter", -3f, 0f);
            var second = SeasonTwoDemoSupport.Spawn(world, "stake", 1.5f, 2f);
            shooter.GetComp<TransformComp>().YawDegrees = 0f;
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, shooter, second, 10f);
            for (int i = 0; i < 20; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(second.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "homing hit");
            CombatLog.Info(CombatCategories.AuraHoming, "AuraHomingDemo PASSED");
        }
    }
}
