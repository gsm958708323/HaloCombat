using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ImmediateOperationTests
    {
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        static SkillSimulation Sim(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillActorSnapshot Caster(SkillSimulation s)=>s.Snapshot().Actors.Single(a=>a.Kind=="caster");
        static void Command(SkillSimulation s,string key)=>s.Execute(new SkillSimulationCommand{Kind="cast",SkillId=key,X=20,Z=0});
        static SkillProjectDocument Shield()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));p.Preview.Recovery=0;
            var s=p.Skills.Single(v=>v.Id=="shield-self");s.Nodes.Single(n=>n.Kind=="cast").Parameters=JObject.FromObject(new{mode="instant",execution="operation",cost=20,cooldown=.5f});return p;
        }
        [Test]
        public void FormalRecastConsumesTheProjectileImmediatelyAndQueuesTeleportWithoutANewAction()
        {
            var p=Formal();p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            var content=CombatContentCompiler.Compile(p);content.Skills.TryGet(new SkillNodeId(2014),out var operation);
            Assert.IsFalse(operation.Timeline.IsValid);Assert.IsNotNull(operation.OperationEffects);Assert.IsFalse(content.Timelines.TryGet(new TimelineId(3014),out _));
            using(var s=Sim(p))
            {
                Command(s,"arena-tracked-shot");s.Advance(26);var projectile=s.Snapshot().Actors.Single(a=>a.Kind=="projectile");
                Command(s,"arena-tracked-shot");Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));Assert.AreEqual(1,s.Snapshot().Statistics.Casts);
                Assert.IsNull(Caster(s).CurrentSkill);Assert.AreEqual(0,Caster(s).X);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="operation"&&e.Step==26));
                s.Advance(1);Assert.AreEqual(projectile.X,Caster(s).X,.00001);Assert.IsNull(Caster(s).CurrentSkill);
                using(var run=new SkillReplayRun(s.CaptureReplay("arena-tracked-shot")))Assert.IsTrue(run.Comparison.Matches);
            }
        }
        [Test]
        public void ABlockedRecastConsumesTheOperationButKeepsTheOriginalProjectileAndTracker()
        {
            var p=Formal();p.Preview.Targets=Array.Empty<PreviewActorDocument>();p.Preview.Caster.CollisionRadius=.6f;
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=1,Z=0,Width=.2f,Depth=2}};
            using(var s=Sim(p))
            {
                Command(s,"arena-tracked-shot");s.Advance(26);var id=s.Snapshot().Actors.Single(a=>a.Kind=="projectile").Id;
                Command(s,"arena-tracked-shot");Assert.IsTrue(s.Snapshot().Actors.Any(a=>a.Id==id));Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="projectile-teleport-blocked"));
                Assert.IsNull(Caster(s).CurrentSkill);s.Advance(1);Assert.AreEqual(0,Caster(s).X);
            }
        }
        [Test]
        public void ImmediateEffectsStillUseCooldownEnergyAndActivityGuards()
        {
            using(var s=Sim(Shield()))
            {
                Assert.IsTrue(s.Cast("shield-self",0,0).Accepted);float shield=Caster(s).Attributes["Shield"];
                Assert.Greater(shield,0);Assert.IsNull(Caster(s).CurrentSkill);Assert.AreEqual(80,s.Snapshot().Energy);Assert.AreEqual(0,s.Snapshot().Statistics.Casts);
                Assert.IsFalse(s.Cast("shield-self",0,0).Accepted);Assert.AreEqual(80,s.Snapshot().Energy);Assert.AreEqual(shield,Caster(s).Attributes["Shield"]);
                s.Advance(30);Assert.IsTrue(s.Cast("shield-self",0,0).Accepted);Assert.AreEqual(shield*2,Caster(s).Attributes["Shield"]);
                s.Interrupt(true);s.Advance(30);Assert.IsFalse(s.Cast("shield-self",0,0).Accepted);Assert.AreEqual(60,s.Snapshot().Energy);
            }
        }
        [Test]
        public void ActorInputRoutingAndOperationSlotsDoNotRequireAFabricatedTimeline()
        {
            var p=Formal();p.Runtime.Actors.Single(a=>a.Id=="arena-player").Slots=new[]{"arena-projectile-warp"};
            var c=CombatContentCompiler.Compile(p);var world=new CombatWorld(c.Runtime.CreateFactory(c.Skills));c.Runtime.Data.Install(world);
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var player);var loadout=player.GetComp<LoadoutComp>();
                Assert.IsTrue(loadout.TryGet(SkillSlot.Normal,out var skill,out var timeline));Assert.AreEqual(2014,skill.Value);Assert.IsFalse(timeline.IsValid);
                Assert.IsFalse(player.GetComp<SkillCastComp>().TryCast(skill,default),"The operation must still validate its required projectile.");
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void BusyAndHitstopGuardsRejectOperationsAndCancelWindowOperationsPreserveThePlayingAction()
        {
            var c=CombatContentCompiler.Compile(Formal());var operation=new SkillNodeId(99990);
            c.Skills.Register(new SkillDefinition{Id=operation,Key="test-operation",EnergyCost=5,TargetMode=SkillTargetMode.Self,OperationEffects=new IEffect[]{new AddShieldEffect(1)}});
            var world=new CombatWorld(c.Runtime.CreateFactory(c.Skills));c.Runtime.Data.Install(world);
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var player);var cast=player.GetComp<SkillCastComp>();var director=player.GetComp<SkillDirectorComp>();
                var aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0);Assert.IsTrue(cast.TryCast(SkillNodeId.G1,aim));int id=director.CurrentOrigin.CastId;
                Assert.IsFalse(cast.TryCast(operation,aim));Assert.AreEqual(100,player.GetComp<EnergyComp>().Current);
                for(int i=0;i<8;i++)world.Tick(.02f);Assert.IsTrue(player.GetComp<TagComp>().Has(CommonTags.Cancel));
                float time=director.TimelineTime;int starts=0;world.Events.Subscribe<EvSkillStarted>(e=>starts++);
                Assert.IsTrue(cast.TryCast(operation,aim));Assert.AreEqual(id,director.CurrentOrigin.CastId);Assert.AreEqual(time,director.TimelineTime);Assert.AreEqual(0,starts);
                world.RequestActorHitstop(player.Id,2);world.Tick(.02f);Assert.IsFalse(cast.TryCast(operation,aim));Assert.AreEqual(95,player.GetComp<EnergyComp>().Current,.0001f);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void OperationProjectilesKeepTheirOwnOriginWhileAnotherActionIsStillPlaying()
        {
            var c=CombatContentCompiler.Compile(Formal());var operation=new SkillNodeId(99991);
            c.Skills.Register(new SkillDefinition{Id=operation,Key="operation-projectile",TargetMode=SkillTargetMode.Facing,OperationEffects=new IEffect[]{new SpawnProjectileEffect(4001)}});
            var world=new CombatWorld(c.Runtime.CreateFactory(c.Skills));c.Runtime.Data.Install(world);
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var player);var cast=player.GetComp<SkillCastComp>();var aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0);
                Assert.IsTrue(cast.TryCast(SkillNodeId.G1,aim));for(int i=0;i<8;i++)world.Tick(.02f);Assert.IsTrue(cast.TryCast(operation,aim));world.Tick(.02f);
                var shot=world.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out var body)&&body.Def.SpecId==4001).GetComp<ProjectileComp>();
                Assert.AreEqual(operation,shot.Family.Origin.Skill);Assert.AreEqual(0,shot.Family.Origin.CastId);Assert.AreEqual(SkillNodeId.G1,player.GetComp<SkillDirectorComp>().CurrentSkill);
            }
            finally{world.Shutdown();}
        }
        [TestCase("channel")] [TestCase("delay")] [TestCase("timeline")] [TestCase("movement")] [TestCase("lifecycle")]
        public void TimedSchedulesCannotBeHiddenInsideAnImmediateOperation(string fault)
        {
            var p=Shield();var s=p.Skills.Single(v=>v.Id=="shield-self");var cast=s.Nodes.Single(n=>n.Kind=="cast");
            if(fault=="channel")cast.Parameters["mode"]="channel";
            if(fault=="delay")cast.Parameters["delay"]=.1f;
            if(fault=="timeline")s.Timeline=new TimelineDocument{RuntimeId=99991,Duration=.2f};
            if(fault=="lifecycle")s.Nodes=s.Nodes.Concat(new[]{new SkillNodeDocument{Id="unused-events",Kind="skill-events"}}).ToArray();
            if(fault=="movement")
            {
                var effect=s.Nodes.Single(n=>n.Kind=="impact");effect.Kind="delivery";effect.RuntimeId=99992;
                effect.Parameters=JObject.FromObject(new{mode="displacement",movementMode="dash",distance=1,speed=1});
            }
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
    }
}
