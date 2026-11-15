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
    public sealed class ScenePlacementTests
    {
        static SkillProjectDocument Project()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SkillSimulation Sim(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillActorSnapshot Caster(SkillSimulation s)=>s.Snapshot().Actors.Single(a=>a.Kind=="caster");
        static void Place(SkillSimulation s,SkillActorSnapshot actor,float x,float z)=>s.Execute(new SkillSimulationCommand{Kind="place",ActorId=actor.SceneId,EntityId=actor.Id,X=x,Z=z});
        [Test]
        public void PlacementUpdatesThePausedSnapshotImmediatelyWithoutReplacingActorsOrAdvancingCombat()
        {
            using(var s=Sim(Project()))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0});s.Advance(10);
                var before=s.Snapshot();var caster=Caster(s);Assert.IsNotNull(caster.ActionTime);var projectile=before.Actors.Single(a=>a.Kind=="projectile");
                Place(s,caster,-5,2);var after=s.Snapshot();Assert.AreEqual(10,after.Step);Assert.AreEqual(-5,Caster(s).X);Assert.AreEqual(2,Caster(s).Z);
                Assert.AreEqual(caster.Id,Caster(s).Id);Assert.AreEqual(caster.ActionTime,Caster(s).ActionTime);Assert.AreEqual(before.Energy,after.Energy);
                Assert.AreEqual(projectile.X,after.Actors.Single(a=>a.Id==projectile.Id).X);Assert.AreEqual(1,after.Statistics.Casts);
                Assert.AreEqual(10,after.Events.Single(e=>e.Kind=="placement").Step);
                using(var replay=new SkillReplayRun(s.CaptureReplay("basic-shot")))Assert.IsTrue(replay.Comparison.Matches);
            }
        }
        [Test]
        public void BlockedOrStaleIdentityPlacementPreservesTheFullSnapshotAndJournal()
        {
            var p=Project();p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=1,Z=0,Width=.5f,Depth=3}};
            using(var s=Sim(p))
            {
                var actor=Caster(s);var before=JObject.FromObject(s.Snapshot());int inputs=s.Recording.Inputs;
                Assert.Throws<ContentValidationException>(()=>Place(s,actor,1,0));
                Assert.Throws<ContentValidationException>(()=>s.Execute(new SkillSimulationCommand{Kind="place",ActorId=actor.SceneId,EntityId="8589934599",X=-5,Z=1}));
                Assert.IsTrue(JToken.DeepEquals(before,JObject.FromObject(s.Snapshot())));Assert.AreEqual(inputs,s.Recording.Inputs);
            }
        }
        [Test]
        public void ARemovedSceneActorCannotRelocateASummonThatReusesItsIndex()
        {
            using(var s=Sim(Project()))
            {
                var target=s.Snapshot().Actors.First(a=>a.Kind=="target");s.Execute(new SkillSimulationCommand{Kind="remove",EntityId=target.Id});s.Advance(1);
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="summon-gunners",X=0,Z=0});s.Advance(10);
                var old=HitboxComp.Unpack(long.Parse(target.Id));
                var reused=s.Snapshot().Actors.Single(a=>HitboxComp.Unpack(long.Parse(a.Id)).Index==old.Index);
                Assert.AreNotEqual(old.Generation,HitboxComp.Unpack(long.Parse(reused.Id)).Generation);Assert.IsNull(reused.SceneId);
                var before=JObject.FromObject(s.Snapshot());Assert.Throws<ContentValidationException>(()=>Place(s,target,9,9));
                Assert.IsTrue(JToken.DeepEquals(before,JObject.FromObject(s.Snapshot())));
            }
        }
        [Test]
        public void ExplicitSetupPlacementReplacesAnOlderUnintegratedGameplayTeleport()
        {
            var p=Project();p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            using(var s=Sim(p))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="tracked-shot",X=20,Z=0});s.Advance(26);
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="tracked-shot",X=20,Z=0});Place(s,Caster(s),2,3);s.Advance(1);
                Assert.AreEqual(2,Caster(s).X);Assert.AreEqual(3,Caster(s).Z);
                using(var replay=new SkillReplayRun(s.CaptureReplay("tracked-shot")))Assert.IsTrue(replay.Comparison.Matches);
            }
        }
        [Test]
        public void PlacementDuringHitstopMovesOnlyTheSetupPoseWithoutConsumingAStopFrame()
        {
            using(var s=Sim(Project()))
            {
                s.Execute(new SkillSimulationCommand{Kind="hitstop",Frames=10});s.Advance(1);var before=s.Snapshot();
                Place(s,Caster(s),-4,2);Assert.AreEqual(before.Step,s.Snapshot().Step);Assert.AreEqual(-4,Caster(s).X);Assert.AreEqual(before.Energy,s.Snapshot().Energy);
            }
        }
        [Test]
        public void AuthoredTeamsChangeRealTargetSelectionAndRemainVisibleInSnapshots()
        {
            var p=Project();p.Preview.Caster.Team=3;p.Preview.Targets=p.Preview.Targets.Take(2).ToArray();
            p.Preview.Targets[0].Team=3;p.Preview.Targets[1].Team=4;
            using(var s=Sim(p))
            {
                Assert.IsTrue(s.Cast("direct-hit",0,0).Accepted);s.Advance(40);var a=s.Snapshot().Actors;
                Assert.AreEqual(3,Caster(s).Team);Assert.AreEqual(1000,a.Single(v=>v.SceneId==p.Preview.Targets[0].Id).Hp);
                Assert.Less(a.Single(v=>v.SceneId==p.Preview.Targets[1].Id).Hp,1000);Assert.AreEqual(4,a.Single(v=>v.SceneId==p.Preview.Targets[1].Id).Team);
            }
        }
        [TestCase(-1)] [TestCase(31)]
        public void InvalidTeamValuesAreRejectedBeforeCreatingTheScene(int team)
        {var p=Project();p.Preview.Targets[0].Team=team;Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));}
        [TestCase("1.5")] [TestCase("\"2\"")] [TestCase("2147483648")]
        public void TeamTokensCannotBeRoundedOrCoercedBeforeValidation(string raw)
        {
            var p=JObject.Parse(SkillProjectJson.Write(Project()));p["preview"]["targets"][0]["team"]=JToken.Parse(raw);
            Assert.Throws<ContentValidationException>(()=>SkillProjectJson.Read(p.ToString()));
        }
    }
}
