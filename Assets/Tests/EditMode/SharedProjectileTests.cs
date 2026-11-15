using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SharedProjectileTests
    {
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        static SkillProjectDocument Examples()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SharedProjectileDocument Definition(SkillProjectDocument p)=>p.Projectiles.Single(d=>d.Id=="arena-normal-projectile");
        static SkillNodeDocument Root(SharedProjectileDocument d)=>d.Nodes.Single(n=>n.Id==d.Root);
        [Test]
        public void TwoRealAxesShareOneProjectileDefinitionAndKeepDifferentEmissionSchedules()
        {
            var p=Formal();var content=CombatContentCompiler.Compile(p);var basic=p.Skills.Single(s=>s.Id=="arena-ammo-shot");var burst=p.Skills.Single(s=>s.Id=="arena-double-shot");
            Assert.AreEqual(6,p.Projectiles.Length);Assert.AreEqual(basic.Nodes.Single(n=>n.Kind=="projectile-ref").Parameters["projectile"],burst.Nodes.Single(n=>n.Kind=="projectile-ref").Parameters["projectile"]);
            Assert.AreEqual(2011,burst.RuntimeId);Assert.AreEqual(3012,burst.Timeline.RuntimeId);Assert.AreEqual(.48f,burst.Timeline.Duration);CollectionAssert.AreEqual(new[]{.08f,.19f},burst.Timeline.Payloads.Select(t=>t.Time));
            Assert.IsTrue(content.Projectiles.TryGet(4001,out var bullet));Assert.AreEqual(6,bullet.Speed);Assert.AreEqual(.1f,bullet.HitRadius);Assert.AreEqual(.1f,bullet.SameTargetDelay);Assert.IsTrue(bullet.PassInvulnerable);
            Root(Definition(p)).Parameters["speed"]=9;Assert.AreEqual(6,bullet.Speed);
        }
        [Test]
        public void RealReturnAndHomingDefinitionsKeepSourceParametersAndBindings()
        {
            var p=Formal();var c=CombatContentCompiler.Compile(p);Assert.IsTrue(c.Projectiles.TryGet(4003,out var b));Assert.IsTrue(b.Motion.SineReturn);Assert.AreEqual(1,b.Motion.ReturnAfter);Assert.AreEqual(5,b.Speed);Assert.AreEqual(.5f,b.HitRadius);Assert.AreEqual(99999,b.MaxHits);Assert.AreEqual(.5f,b.SameTargetDelay);Assert.IsFalse(b.RemoveOnObstacle);
            Assert.IsTrue(c.Projectiles.TryGet(4006,out var homing));Assert.AreEqual(36000,homing.HomingRate);Assert.AreEqual(14,homing.HomingAcquireRadius);Assert.AreEqual(100,homing.Lifetime);
            Assert.AreEqual("arena-homing-shot",p.Runtime.Combos.Single(r=>r.Id=="ArenaHoming").ToSkill);Assert.AreEqual("arena-returning-shot",p.Runtime.Combos.Single(r=>r.Id=="ArenaArea").ToSkill);
            Assert.AreEqual("arena-enemy-shot",p.Runtime.Actors.Single(a=>a.Id=="arena-enemy").SkillId);
        }
        [TestCase("missing")] [TestCase("root")] [TestCase("kind")] [TestCase("disabled")] [TestCase("duplicate")] [TestCase("number")] [TestCase("emission")] [TestCase("root-event")]
        public void SharedProjectileReferencesAndDefinitionsRejectInvalidBoundaries(string fault)
        {
            var p=Formal();var d=Definition(p);var root=Root(d);
            if(fault=="missing")p.Skills.Single(s=>s.Id=="arena-double-shot").Nodes.Single(n=>n.Kind=="projectile-ref").Parameters["projectile"]="missing";
            if(fault=="root")d.Root="missing";
            if(fault=="kind")root.Kind="damage";
            if(fault=="disabled")root.Enabled=false;
            if(fault=="duplicate")p.Projectiles=p.Projectiles.Concat(new[]{d}).ToArray();
            if(fault=="number")Root(p.Projectiles[1]).RuntimeId=root.RuntimeId;
            if(fault=="emission")root.Parameters["multi"]=false;
            if(fault=="root-event")d.Nodes=d.Nodes.Concat(new[]{new SkillNodeDocument{Id="root-events",Kind="buff-events"}}).ToArray();
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void ForwardProjectileReferencesResolveAndCyclesAreRejected()
        {
            var p=Formal();var d=Definition(p);var refNode=new SkillNodeDocument{Id="child-reference",Kind="projectile-ref",Parameters=JObject.FromObject(new{projectile="arena-homing-projectile",count=1,spread=0})};
            d.Nodes=d.Nodes.Concat(new[]{refNode}).ToArray();d.Edges=d.Edges.Concat(new[]{new SkillEdgeDocument{Id="forward-ref",Source=d.Root,SourcePort="hit",Target=refNode.Id,TargetPort="execute",Order=3}}).ToArray();
            Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(p));refNode.Parameters["projectile"]=d.Id;StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [Test]
        public void PublicationClosureContainsOneDefinitionAndItsActualBuffDependencies()
        {
            var p=Formal();var pub=SkillPublicationJson.Create(p,"arena-double-shot");var read=SkillPublicationJson.Read(SkillPublicationJson.Write(pub));
            CollectionAssert.AreEqual(new[]{"arena-double-shot"},read.Document.Project.Skills.Select(s=>s.Id));Assert.AreEqual("arena-normal-projectile",read.Document.Project.Projectiles.Single().Id);Assert.AreEqual("arena-vulnerability",read.Document.Project.Buffs.Single().Id);
            Assert.IsTrue(pub.Project.Projectiles[0].Nodes.All(n=>n.Position.X==0&&n.Position.Y==0));Assert.Greater(Definition(p).Nodes.Max(n=>n.Position.X),0);
            var shared=SkillPublicationJson.Create(p,"arena-combo-second");Assert.AreEqual(1,shared.Project.Timelines.Length);Assert.AreEqual("arena-confirmed-projectile",shared.Project.Projectiles.Single().Id);
        }
        [Test]
        public void PublishedSharedProjectileConflictsKeepTheOldSessionAndArena()
        {
            var p=Formal();string first=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-ammo-shot"));string second=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-double-shot"));
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",first},{"f7",second}});var previous=session.Simulation;
                Root(Definition(p)).Parameters["speed"]=9;second=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-double-shot"));
                StringAssert.Contains("projectile versions",Assert.Throws<ContentValidationException>(()=>session.Replace(new Dictionary<string,string>{{"f6",first},{"f7",second}})).Message);Assert.AreSame(previous,session.Simulation);
                Assert.Throws<ContentValidationException>(()=>new ArenaProject(Formal(),new Dictionary<string,string>{{"f6",first},{"f7",second}}));
            }
        }
        [Test]
        public void LayoutDoesNotChangeThePublishedVersionAndSemanticEditsDo()
        {
            var p=Formal();string version=SkillPublicationJson.Create(p,"arena-double-shot").Revision;Root(Definition(p)).Position.X=999;
            Assert.AreEqual(version,SkillPublicationJson.Create(p,"arena-double-shot").Revision);Root(Definition(p)).Parameters["speed"]=9;Assert.AreNotEqual(version,SkillPublicationJson.Create(p,"arena-double-shot").Revision);
        }
        [TestCase("negative")] [TestCase("duplicate")] [TestCase("unsorted")] [TestCase("empty")] [TestCase("type")] [TestCase("nan")]
        public void ScheduledBounceTimesAreStrictlyTypedAndIncreasing(string fault)
        {
            var p=Examples();var n=p.Skills.Single(s=>s.Id=="phase-bounce").Nodes.Single(n=>n.Kind=="projectile");
            n.Parameters["groundPhases"]=fault=="negative"?new JArray(-1,2):fault=="duplicate"?new JArray(1,1):fault=="unsorted"?new JArray(2,1):fault=="empty"?new JArray():fault=="type"?new JArray("1",2):new JArray(double.NaN,2);
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void ScheduledCurveCopiesItsTimesAndOldPublicationsStillValidate()
        {
            var p=Examples();var c=CombatContentCompiler.Compile(p);Assert.IsTrue(c.Projectiles.TryGet(6505,out var def));var times=def.Motion.ScheduledBounce.Phases;
            p.Skills.Single(s=>s.Id=="phase-bounce").Nodes.Single(n=>n.Kind=="projectile").Parameters["groundPhases"][0]=2;
            Assert.AreEqual(1,times[0]);string old=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/f6.json"));Assert.DoesNotThrow(()=>SkillPublicationJson.Read(old));
        }
        [TestCase("skill",HitFeedbackLevel.Finisher,2,4)]
        [TestCase("none",HitFeedbackLevel.None,1,1)]
        public void SharedDamageInheritsTheCastingSkillUnlessExplicitlyDisabled(string feedback,HitFeedbackLevel expected,int sourceFrames,int targetFrames)
        {
            var p=Formal();
            p.Skills.Single(s=>s.Id=="arena-ammo-shot").Nodes.Single(n=>n.Kind=="cast").Parameters["hitFeedback"]="finisher";
            var damage=Definition(p).Nodes.Single(n=>n.Kind=="damage");damage.Parameters["feedback"]=feedback;damage.Parameters["feedbackHitstop"]=true;
            damage.Parameters["sourceHitstopFrames"]=1;damage.Parameters["targetHitstopFrames"]=1;
            var content=CombatContentCompiler.Compile(p);var world=new CombatWorld(content.Runtime.CreateFactory(content.Skills),random:new FixedRandom(.9f));world.ReplaceCatalogs(content.Projectiles,content.Aoes,content.Summons);
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var source);world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-target")),out var target);target.GetComp<TransformComp>().Position=new SimVec3(1,0,0);
                var hits=new List<EvDamage>();var stops=new List<EvHitstop>();world.Events.Subscribe<EvDamage>(e=>hits.Add(e));world.Events.Subscribe<EvHitstop>(e=>stops.Add(e));
                Assert.IsTrue(source.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2001),new SkillAim(target.Id,new SimVec3(1,0,0),0)));for(int i=0;i<35;i++)world.Tick(.02f);
                Assert.AreEqual(expected,hits.Single().Result.Feedback);Assert.AreEqual(sourceFrames,stops.Single().SourceFrames);Assert.AreEqual(targetFrames,stops.Single().TargetFrames);
            }
            finally{world.Shutdown();}
        }
    }
}
