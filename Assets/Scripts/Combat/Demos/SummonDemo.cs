using Combat.Core;

namespace Combat.Demos
{
    public static class SummonDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            EntityId summonSource = EntityId.Invalid;
            bool summonHit = false;
            events.Subscribe<EvDamage>(e => { if (summonSource.IsValid && e.Source == summonSource) summonHit = true; });
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var owner = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var stake = SeasonTwoDemoSupport.Spawn(world, "stake", 1f, 0f);
            world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, owner, null, 0f, owner.GetComp<TransformComp>().Position);
            Actor summon = null;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++) if (actors[i].TryGetComp<SummonComp>(out _)) summon = actors[i];
            SeasonTwoDemoSupport.Assert(summon != null, "summon spawn");
            SeasonTwoDemoSupport.Assert(summon.GetComp<SummonComp>().OwnerId == owner.Id, "summon owner");
            SeasonTwoDemoSupport.Assert(summon.GetComp<BehaviorTreeComp>().Board.Owner == owner.Id, "owner reaches bt board");
            SeasonTwoDemoSupport.Assert(!summon.TryGetComp<InputBufferComp>(out _) && !summon.TryGetComp<ComboComp>(out _), "summon has no player input path");
            summonSource = summon.Id;
            for (int i = 0; i < 120; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(stake.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "summon bt attack");
            SeasonTwoDemoSupport.Assert(summonHit, "summon is damage source");
            owner.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "OwnerDead" });
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(!summon.IsActive && !world.TryGetActor(summon.Id, out _) && stake.IsActive, "owner cleanup summon");
            CombatLog.Info(CombatCategories.Summon, "SummonDemo PASSED");
        }
    }
}
