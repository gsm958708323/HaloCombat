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
    public sealed class RestoredAreaTests
    {
        static SkillProjectDocument Project(string area)
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            p.Preview.Caster.X=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=1,Hp=1000}};
            var s=p.Skills.Single(v=>v.Id=="arena-projectile-warp");s.Nodes=s.Nodes.Where(n=>n.Kind!="teleport-projectile").ToArray();
            s.Nodes.Single(n=>n.Kind=="cast").Parameters=JObject.FromObject(new{mode="instant",execution="operation",cost=0,cooldown=0});
            s.Nodes=s.Nodes.Concat(new[]{new SkillNodeDocument{Id="restored-area",Kind="area-ref",Parameters=JObject.FromObject(new{area,center="source"})}}).ToArray();
            s.Edges=s.Edges.Where(e=>e.Target!="warp").Concat(new[]{new SkillEdgeDocument{Id="spawn",Source="cast",SourcePort="cast",Target="restored-area",TargetPort="execute"}}).ToArray();return p;
        }
        [Test]
        public void OriginalBlackHolePullUsesItsSourceDistanceCurveForEveryFixedStepThenExpires()
        {
            var p=Project("arena-black-hole");using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                Assert.IsTrue(s.Cast("arena-projectile-warp",0,0).Accepted);float expected=1;
                for(int step=0;step<50;step++){expected-=expected/(expected+1)*.02f;s.Advance(1);}
                Assert.AreEqual(expected,s.Snapshot().Actors.Single(a=>a.Name=="target").X,.00001f);
                Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="aoe"));float position=expected;s.Advance(10);Assert.AreEqual(position,s.Snapshot().Actors.Single(a=>a.Name=="target").X,.00001f);
            }
        }
        [Test]
        public void PermanentShieldAbsorbsFriendlyProjectilesGrowsAndCleansUpWithItsOwner()
        {
            var p=Project("arena-absorbing-shield");p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                Assert.IsTrue(s.Cast("arena-projectile-warp",0,0).Accepted);Assert.IsTrue(s.Cast("arena-tracked-shot",20,0).Accepted);s.Advance(6);
                var shield=s.Snapshot().Actors.Single(a=>a.Kind=="aoe");Assert.AreEqual(1,shield.AbsorbedProjectiles);Assert.AreEqual(1.5f*1.05f,shield.Radius,.00001f);
                Assert.AreEqual(0,shield.AreaVelocityX);Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));s.Advance(500);Assert.IsTrue(s.Snapshot().Actors.Any(a=>a.Kind=="aoe"));
                s.Remove(s.Snapshot().Actors.Single(a=>a.Kind=="caster").Id);Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="aoe"));
            }
        }
        [TestCase("arena-absorbing-shield",4101)] [TestCase("arena-black-hole",4103)]
        public void RestoredDefinitionsParticipateInClosureAndJsonRoundtrip(string area,int id)
        {
            var p=Project(area);var text=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-projectile-warp"));var loaded=SkillPublicationJson.Read(text);
            Assert.AreEqual(1,loaded.Document.Project.Areas.Length);Assert.IsTrue(loaded.Content.Aoes.TryGet(id,out _));
            using(var s=new SkillSimulation(loaded.Document.Project,loaded.Content))
            {s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="arena-projectile-warp",X=0,Z=0});s.Advance(30);using(var replay=new SkillReplayRun(s.CaptureReplay("arena-projectile-warp")))Assert.IsTrue(replay.Comparison.Matches);}
        }
    }
}
