using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SwordMechanicsTests
    {
        static readonly SkillNodeId Skill = new SkillNodeId(720);
        static readonly InputToken ReturnInput = new InputToken("SwordStep");
        static readonly InputToken OtherInput = new InputToken("OtherStep");
        static readonly SkillAim SelfAim = new SkillAim(EntityId.Invalid, SimVec3.Zero, 0);
        static SkillProjectDocument Example() => SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
        static SkillNodeDocument Node(SkillProjectDocument project, string skill, string kind)
            => project.Skills.Single(value => value.Id == skill).Nodes.Single(value => value.Kind == kind);

        sealed class EffectCounter : IEffect
        {
            public int Calls { get; private set; }
            public void Apply(ref EffectContext context) => Calls++;
        }

        static (CombatWorld World, Actor Actor) CreateActor(bool operation = false)
        {
            var skills = new SkillCatalog();
            var timelines = new TimelineLibrary();
            var timeline = new TimelineId(Skill.Value);
            timelines.Register(new TimelineDefinition { Id = timeline, Duration = .1f });
            skills.Register(new SkillDefinition
            {
                Id = Skill, Timeline = operation ? default : timeline, TargetMode = SkillTargetMode.Self,
                Cooldown = 2, ReturnWindow = 4, EnergyCost = 4, AmmoCost = 1, RequiredBuffId = 42, BuffStackCost = 2,
                OperationEffects = operation ? new IEffect[] { new EffectCounter() } : null
            });
            var data = new BakedCombatData
            {
                Timelines = timelines,
                Combo = new ComboTableDefinition { Entries = new[]
                {
                    new ComboEntry { Key = "step", Input = ReturnInput, ToSkill = Skill },
                    new ComboEntry { Key = "other", Input = OtherInput, ToSkill = Skill }
                } }
            };
            var factory = new RuntimeActorFactory(data, skills, new Dictionary<string, RuntimeActorDefinition>
            {
                ["player"] = new RuntimeActorDefinition
                {
                    Role = "player", Team = 1, Hp = 100, CollisionRadius = .6f,
                    Energy = 10, InitialEnergy = 10, Ammo = 3, InitialAmmo = 3
                }
            });
            var world = new CombatWorld(factory, obstacles:new[] { new ObstacleDefinition("edge", 1, 0, 1, 1) });
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("player")), out var actor);
            return (world, actor);
        }

        [Test]
        public void StackConsumptionIsAtomicAndPreservesPartialInstanceContributions()
        {
            var (world, actor) = CreateActor();
            var buffs = actor.GetComp<BuffComp>();
            var tag = new TagId(6231);
            var onRemoved = new EffectCounter();
            var onExpire = new EffectCounter();
            var spec = new DurationSpec
            {
                BuffId = 42, Stack = StackPolicy.Independent, MaxStacks = 5, Duration = 1,
                Modifiers = new[] { new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 5 } },
                GrantedTags = new[] { tag }, OnRemoved = new IEffect[] { onRemoved }, OnExpire = new IEffect[] { onExpire }
            };
            buffs.Apply(spec, actor, 2);
            buffs.Apply(spec, actor, 3);
            world.Tick(.02f);
            var second = buffs.Snapshot()[1];
            Assert.That(buffs.TryConsumeStacks(42, 6), Is.False);
            Assert.That(buffs.StacksOf(42), Is.EqualTo(5));
            var events = new List<EvBuffLifecycle>();
            world.Events.Subscribe<EvBuffLifecycle>(events.Add);
            Assert.That(buffs.TryConsumeStacks(42, 4), Is.True);
            var left = buffs.Snapshot().Single();
            Assert.That(left.InstanceId, Is.EqualTo(second.InstanceId));
            Assert.That(left.Stacks, Is.EqualTo(1));
            Assert.That(left.Remaining, Is.EqualTo(second.Remaining));
            Assert.That(actor.GetComp<TagComp>().Stack(tag), Is.EqualTo(1));
            Assert.That(actor.GetComp<AttributeSet>().ModCount, Is.EqualTo(1));
            Assert.That(onRemoved.Calls, Is.EqualTo(1));
            Assert.That(onExpire.Calls, Is.Zero);
            Assert.That(events.Select(e => e.Kind), Is.EqualTo(new[] { BuffEventKind.Removed, BuffEventKind.Consumed }));
            Assert.That(events.All(e => e.Reason == BuffRemovalReason.Consumed), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CastDebitsOnceAndBlockedReturnKeepsItsAnchor(bool operation)
        {
            var (world, actor) = CreateActor(operation);
            var cast = actor.GetComp<SkillCastComp>();
            var buffs = actor.GetComp<BuffComp>();
            var ownerCast = new EffectCounter();
            Assert.That(cast.TryCast(Skill, SelfAim), Is.False);
            Assert.That(cast.LastRejection, Does.Contain("buff"));
            Assert.That(cast.CooldownLeft(Skill), Is.Zero);
            Assert.That(actor.GetComp<EnergyComp>().Current, Is.EqualTo(10));
            Assert.That(actor.GetComp<AmmoComp>().Current, Is.EqualTo(3));
            buffs.Apply(new DurationSpec { BuffId = 42, MaxStacks = 3, OnOwnerCast = new IEffect[] { ownerCast } }, actor, 2);
            Assert.That(cast.TryCast(Skill, SelfAim), Is.True);
            Assert.That(buffs.StacksOf(42), Is.Zero);
            Assert.That(ownerCast.Calls, Is.Zero);
            if (!operation)
            {
                Assert.That(cast.TryCast(Skill, SelfAim), Is.False);
                Assert.That(cast.LastRejection, Does.Contain("idle"));
            }
            for (int i = 0; i < 6; i++) world.Tick(.02f);
            var motor = actor.GetComp<LocomotionComp>();
            Assert.That(motor.TryReposition(new SimVec3(-4, 0, 0)), Is.True);
            float remaining = cast.CooldownLeft(Skill);
            Assert.That(cast.ReturnAnchors().Single().Remaining, Is.EqualTo(remaining));
            Assert.That(cast.TryCast(Skill, SelfAim), Is.False);
            Assert.That(cast.LastRejection, Does.Contain("destination"));
            Assert.That(cast.ReturnAnchors(), Has.Length.EqualTo(1));
            motor.CollisionRadius = .2f;
            Assert.That(cast.TryCast(Skill, SelfAim), Is.True);
            Assert.That(cast.CooldownLeft(Skill), Is.EqualTo(remaining));
            Assert.That(actor.GetComp<EnergyComp>().Current, Is.EqualTo(6));
            Assert.That(actor.GetComp<AmmoComp>().Current, Is.EqualTo(2));
            Assert.That(cast.ReturnAnchors(), Is.Empty);
            world.Tick(.02f);
            Assert.That(actor.GetComp<TransformComp>().Position.X, Is.Zero);
        }

        [Test]
        public void OriginReturnRequiresTheInputThatArmedItAndDoesNotFireWhileHeld()
        {
            var (world, actor) = CreateActor();
            var cast = actor.GetComp<SkillCastComp>();
            var buffer = actor.GetComp<InputBufferComp>();
            var aiming = actor.GetComp<PlayerAimComp>();
            var motor = actor.GetComp<LocomotionComp>();
            motor.CollisionRadius = .2f;
            actor.GetComp<BuffComp>().Apply(new DurationSpec { BuffId = 42, MaxStacks = 2 }, actor, 2);
            var returns = new List<EvSkillReturned>();
            world.Events.Subscribe<EvSkillReturned>(returns.Add);
            buffer.Push(ReturnInput);
            world.Tick(.02f);
            aiming.PrimaryHeld = true;
            aiming.PrimaryToken = ReturnInput;
            for (int i = 0; i < 8; i++) world.Tick(.02f);
            Assert.That(returns, Is.Empty);
            Assert.That(cast.ReturnAnchors().Single().Input, Is.EqualTo(ReturnInput));
            Assert.That(motor.TryReposition(new SimVec3(-4, 0, 0)), Is.True);
            buffer.Push(OtherInput);
            world.Tick(.02f);
            Assert.That(actor.GetComp<TransformComp>().Position.X, Is.EqualTo(-4));
            Assert.That(returns, Is.Empty);
            buffer.Clear();
            buffer.Push(ReturnInput);
            world.Tick(.02f);
            Assert.That(actor.GetComp<TransformComp>().Position.X, Is.Zero);
            Assert.That(returns.Single().Origin.Skill, Is.EqualTo(Skill));
            Assert.That(returns.Single().Origin.CastId, Is.GreaterThan(0));
            Assert.That(buffer.HasBuffered, Is.False);
        }

        [Test]
        public void ReturnWindowClosesAtTheOriginalCooldownAndPreservesNormalCasting()
        {
            var (world, actor) = CreateActor(operation:true);
            var cast = actor.GetComp<SkillCastComp>();
            var buffs = actor.GetComp<BuffComp>();
            var spec = new DurationSpec { BuffId = 42, MaxStacks = 2 };
            buffs.Apply(spec, actor, 2);
            Assert.That(cast.TryCast(Skill, SelfAim), Is.True);
            world.Tick(2);
            Assert.That(cast.CooldownLeft(Skill), Is.Zero);
            Assert.That(cast.ReturnAnchors(), Is.Empty);
            Assert.That(cast.TryCast(Skill, SelfAim), Is.False);
            Assert.That(cast.LastRejection, Does.Contain("buff"));
            buffs.Apply(spec, actor, 2);
            Assert.That(cast.TryCast(Skill, SelfAim), Is.True);
            Assert.That(buffs.StacksOf(42), Is.Zero);
        }

        [Test]
        public void CastBuffReferenceIsPublishedAndInvalidGuardFieldsAreRejected()
        {
            var project = Example();
            var parameters = Node(project, "ground-field", "cast").Parameters;
            parameters["buffStackCost"] = 2;
            Assert.That(Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path,
                Does.EndWith(".buffStackCost"));
            parameters["requiredBuff"] = "missing";
            Assert.That(Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path,
                Does.EndWith(".requiredBuff"));
            parameters["requiredBuff"] = "battle-rage";
            parameters["buffStackCost"] = 1.5;
            Assert.That(Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path,
                Does.EndWith(".buffStackCost"));
            parameters.Remove("buffStackCost");
            var publication = SkillPublicationJson.Create(project, "ground-field");
            Assert.That(publication.Project.Buffs.Select(buff => buff.Id), Is.EqualTo(new[] { "battle-rage" }));
            using var simulation = new SkillSimulation(project, CombatContentCompiler.Compile(project));
            Assert.That(simulation.Cast("ground-field", 0, 0).Accepted, Is.False);
            Assert.That(simulation.Cast("battle-rage", 0, 0).Accepted, Is.True);
            simulation.Advance(20);
            Assert.That(simulation.Cast("ground-field", 0, 0).Accepted, Is.True);
            Assert.That(simulation.Snapshot().Actors.Single(actor => actor.Name == "caster").Buffs.Single().Stacks, Is.EqualTo(1));
        }

        [Test]
        public void AnnulusFiltersBeforeTargetCapAndTracksRingOccupancy()
        {
            var project = Example();
            var skill = project.Skills.Single(value => value.Id == "ground-field");
            var parameters = Node(project, skill.Id, "delivery").Parameters;
            parameters["innerRadius"] = 4;
            Assert.That(Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path,
                Does.EndWith(".innerRadius"));
            parameters["innerRadius"] = 2;
            parameters["persistent"] = false;
            Assert.That(Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path,
                Does.EndWith(".innerRadius"));
            parameters["persistent"] = true;
            parameters["maxTargets"] = 1;
            skill.Edges = skill.Edges.Concat(new[]
            {
                new SkillEdgeDocument { Id = "annulus-enter", Source = "area", SourcePort = "enter", Target = "damage", TargetPort = "execute" },
                new SkillEdgeDocument { Id = "annulus-exit", Source = "area", SourcePort = "exit", Target = "damage", TargetPort = "execute" }
            }).ToArray();
            using var simulation = new SkillSimulation(project, CombatContentCompiler.Compile(project));
            Assert.That(simulation.Cast("ground-field", 0, 0).Accepted, Is.True);
            simulation.Advance(8);
            var snapshot = simulation.Snapshot();
            Assert.That(snapshot.Actors.Single(actor => actor.Kind == "aoe").InnerRadius, Is.EqualTo(2));
            Assert.That(snapshot.Actors.Single(actor => actor.Name == "target-1").Hp, Is.EqualTo(1000));
            Assert.That(snapshot.Actors.Single(actor => actor.Name == "target-2").Hp, Is.LessThan(1000));
            simulation.Place("target-2", 1, 0);
            simulation.Place("target-1", 2, 0);
            simulation.Advance(15);
            snapshot = simulation.Snapshot();
            Assert.That(snapshot.Events.Count(e => e.Kind == "area-enter"), Is.EqualTo(2));
            Assert.That(snapshot.Events.Count(e => e.Kind == "area-exit"), Is.EqualTo(1));
            Assert.That(snapshot.Actors.Single(actor => actor.Name == "target-1").Hp, Is.LessThan(1000));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PortableReplayOnlyAddsGeometryAndAnchorFieldsWhenAuthored(bool optIn)
        {
            var project = Example();
            if (optIn)
            {
                Node(project, "ground-field", "cast").Parameters["returnWindow"] = .5;
                Node(project, "ground-field", "delivery").Parameters["innerRadius"] = 2;
            }
            using var simulation = new SkillSimulation(project, CombatContentCompiler.Compile(project));
            simulation.Execute(new SkillSimulationCommand { Kind = "cast", SkillId = "ground-field", X = 0, Z = 0 });
            simulation.Advance(8);
            var document = simulation.CaptureReplay("ground-field");
            var actors = document.ExpectedState["actors"];
            Assert.That(actors.Any(actor => actor["returnAnchors"] != null), Is.EqualTo(optIn));
            Assert.That(actors.Any(actor => actor["innerRadius"] != null), Is.EqualTo(optIn));
            AssertReplayMatches(document);
            if (!optIn) return;

            simulation.Advance(12);
            simulation.Execute(new SkillSimulationCommand { Kind = "place", ActorId = "caster", X = -4, Z = 0 });
            var returned = (SkillCastResult)simulation.Execute(new SkillSimulationCommand { Kind = "cast", SkillId = "ground-field", X = 999, Z = 999 });
            Assert.That(returned.Accepted, Is.True);
            simulation.Advance(1);
            var snapshot = simulation.Snapshot();
            Assert.That(snapshot.Actors.Single(actor => actor.Name == "caster").X, Is.EqualTo(project.Preview.Caster.X));
            Assert.That(snapshot.Events.Count(e => e.Kind == "skill-return"), Is.EqualTo(1));
            Assert.That(snapshot.Statistics.Casts, Is.EqualTo(1));
            AssertReplayMatches(simulation.CaptureReplay("ground-field"));
        }

        static void AssertReplayMatches(SkillReplayDocument document)
        {
            using var replay = new SkillReplayRun(document);
            Assert.That(replay.Comparison.Matches, Is.True,
                string.Join(", ", replay.Comparison.Differences.Select(e => e.Path)));
        }
    }
}
