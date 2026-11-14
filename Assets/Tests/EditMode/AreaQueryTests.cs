using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class AreaQueryTests
    {
        sealed class Factory : IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var actor = new Actor(); actor.AddComp(new TransformComp()); actor.AddComp(new TagComp());
                actor.AddComp(new TeamComp(spec.BlueprintId == "owner" ? 1 : 2));
                if (spec.BlueprintId == "aoe") actor.AddComp(new AoeComp());
                else if (spec.BlueprintId == "projectile") actor.AddComp(new ProjectileComp());
                else { var attr = new AttributeSet(); attr.InitFighterDefaults(); actor.AddComp(attr); actor.AddComp(new HealthComp()); }
                return actor;
            }
            public void Release(Actor actor) => actor.ResetForPool();
        }
        static Actor Spawn(CombatWorld world, string blueprint, float x = 0)
        {
            var id = world.SpawnActor(new ActorSpawnSpec(blueprint)); world.TryGetActor(id, out var actor);
            actor.GetComp<TransformComp>().Position = new SimVec3(x, 0, 0); return actor;
        }
        static DamageEffect Damage(float value) => new DamageEffect { Flat = value, Coeff = 0, CanCrit = false, FireOnHurted = false };

        [Test]
        public void UnlimitedAreaHitsAll64TargetsAndFiltersAlliesDeadAndRuntimeBodies()
        {
            var world = new CombatWorld(new Factory()); var owner = Spawn(world, "owner");
            var targets = Enumerable.Range(0, 64).Select(_ => Spawn(world, "target")).ToArray();
            var ally = Spawn(world, "owner"); var dead = Spawn(world, "target");
            dead.GetComp<TagComp>().Add(CommonTags.Dead, 1, TagSource.Effect("Test.Dead"));
            Spawn(world, "aoe"); Spawn(world, "projectile");
            world.Deliver(new IEffect[] { new CircleEffects(new IEffect[] { Damage(1) }, EffectCenter.Source, 3) }, owner, null, 0);
            foreach (var target in targets) Assert.AreEqual(99, target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            Assert.AreEqual(100, ally.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            Assert.AreEqual(100, dead.GetComp<AttributeSet>().GetBase(AttrId.Hp));
        }

        [Test]
        public void NestedAreaQueriesDoNotOverwriteTheOuterTargetSetOrRegistrySnapshot()
        {
            var world = new CombatWorld(new Factory()); var owner = Spawn(world, "owner");
            var targets = Enumerable.Range(1, 4).Select(x => Spawn(world, "target", x)).ToArray();
            var snapshot = world.RegistryActive();
            var nested = new CircleEffects(new IEffect[] { Damage(2) }, EffectCenter.Target, .1f);
            world.Deliver(new IEffect[] { new CircleEffects(new IEffect[] { nested, Damage(1) }, EffectCenter.Source, 5) }, owner, null, 0);
            foreach (var target in targets) Assert.AreEqual(97, target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            Spawn(world, "target", 10); world.RegistryActive();
            Assert.AreEqual(5, snapshot.Count, "A prior registry snapshot cannot be rewritten by nested calls.");
        }

        sealed class NestedWorldPulse : IEffect
        {
            public CombatWorld World; public Actor Area; public AoeComp Body;
            public void Apply(ref EffectContext context) => AoePulse.PulseNow(World, Area, Body);
        }

        [Test]
        public void ReentrantAoePulseInAnotherWorldCannotReplaceOuterWorldVictims()
        {
            var first = new CombatWorld(new Factory()); var second = new CombatWorld(new Factory());
            var firstOwner = Spawn(first, "owner"); var secondOwner = Spawn(second, "owner");
            var firstTargets = Enumerable.Range(0, 3).Select(_ => Spawn(first, "target")).ToArray();
            var secondTarget = Spawn(second, "target");
            var inner = Spawn(second, "aoe"); var body = inner.GetComp<AoeComp>();
            body.Setup(new AoeDefinition { SpecId = 1, Radius = 2, OnPulse = new IEffect[] { Damage(1) } }, secondOwner.Id, 0, 0);
            var outer = Spawn(first, "aoe"); var outerBody = outer.GetComp<AoeComp>();
            outerBody.Setup(new AoeDefinition { SpecId = 2, Radius = 2, OnPulse = new IEffect[]
            { new NestedWorldPulse { World = second, Area = inner, Body = body }, Damage(2) } }, firstOwner.Id, 0, 0);
            AoePulse.PulseNow(first, outer, outerBody);
            foreach (var target in firstTargets) Assert.AreEqual(98, target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            Assert.AreEqual(97, secondTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp));
        }
    }
}
