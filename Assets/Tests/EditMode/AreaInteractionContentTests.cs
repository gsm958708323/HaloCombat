using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class AreaInteractionContentTests
    {
        static SkillProjectDocument Example()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SkillNodeDocument Orb(SkillProjectDocument p)=>p.Skills.Single(s=>s.Id=="absorbing-orb").Nodes.Single(n=>n.Id=="orb");
        [Test]
        public void FormalMonkeyInputUsesTheOriginalSkillTimelineAndAreaValues()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContentFile)));
            var content=CombatContentCompiler.Compile(p);var skill=content.Skills.All.Single(s=>s.Key=="arena-absorbing-orb");
            Assert.AreEqual(2003,skill.Id.Value);Assert.AreEqual(3,skill.AmmoCost);Assert.AreEqual(3003,skill.Timeline.Value);
            Assert.IsTrue(content.Timelines.TryGet(skill.Timeline,out var timeline));Assert.AreEqual(.5f,timeline.Duration);Assert.AreEqual(.1f,timeline.Payloads.Single().Time);Assert.AreEqual("Fire",timeline.Animation);
            Assert.IsTrue(content.Aoes.TryGet(4102,out var area));Assert.AreEqual(.25f,area.Radius);Assert.AreEqual(100,area.Duration);Assert.AreEqual(.45f,area.PulseInterval);
            Assert.IsTrue(area.MoveForward);Assert.AreEqual(.1f,area.MoveSpeed);Assert.IsTrue(area.RemoveOnObstacle);Assert.IsTrue(area.AbsorbProjectiles);Assert.AreEqual(.05f,area.ProjectileAbsorbForce);Assert.AreEqual(.05f,area.ProjectileRadiusScale);
            Assert.AreEqual("ArenaOrb",p.Runtime.Arena.Bindings.Single(b=>b.Action=="Monkey").Input);Assert.IsTrue(p.Runtime.Combos.Any(c=>c.Input=="ArenaOrb"&&c.ToSkill=="arena-absorbing-orb"));
        }
        const string UnityRuntimeContentFile="Combat/project.json";
        [TestCase("moveSpeed",-1)] [TestCase("forwardOffset",-1)] [TestCase("absorbForce",-1)] [TestCase("radiusPerProjectile",-1)]
        public void InvalidMotionAndAbsorptionNumbersAreRejected(string field,float value)
        {var p=Example();Orb(p).Parameters[field]=value;StringAssert.Contains(field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);}
        [Test]
        public void UnknownMotionAndUnavailableAbsorptionPortsFailExplicitly()
        {
            var p=Example();Orb(p).Parameters["areaMotion"]="unknown";Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
            p=Example();var skill=p.Skills.Single(s=>s.Id=="absorbing-orb");var cue=new SkillNodeDocument{Id="absorbed-cue",Kind="cue",Parameters=Newtonsoft.Json.Linq.JObject.FromObject(new{id=101,name="Absorb"})};
            skill.Nodes=skill.Nodes.Concat(new[]{cue}).ToArray();skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id="absorb",Source="orb",SourcePort="absorb",Target=cue.Id,TargetPort="execute"}}).ToArray();
            Orb(p).Parameters["absorbProjectiles"]=false;StringAssert.Contains("absorb",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
            Orb(p).Parameters["absorbProjectiles"]=true;Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void PureAbsorptionAreaCanBePermanentAndTheSnapshotUsesAnUnboundedLifetime()
        {
            var p=Example();var s=p.Skills.Single(v=>v.Id=="absorbing-orb");s.Nodes=s.Nodes.Where(n=>n.Id!="damage"&&n.Id!="hurt").ToArray();s.Edges=s.Edges.Where(e=>e.Source!="orb"&&e.Source!="damage").ToArray();Orb(p).Parameters["permanent"]=true;
            var content=CombatContentCompiler.Compile(p);using(var simulation=new SkillSimulation(p,content))
            {Assert.IsTrue(simulation.Cast(s.Id,10,0).Accepted);simulation.Advance(10);var area=simulation.Snapshot().Actors.Single(a=>a.Kind=="aoe");Assert.IsNull(area.Remaining);Assert.AreEqual(.25f,area.Radius);Assert.Greater(area.X,p.Preview.Caster.X+.5f);}
        }
        [TestCase(-1)] [TestCase(3601)]
        public void InvalidRepeatedContactDelayFailsBeforeAnyCast(float delay)
        {var p=Example();p.Skills.Single(s=>s.Id=="repeat-contact").Nodes.Single(n=>n.Kind=="projectile").Parameters["sameTargetDelay"]=delay;Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));}
        [Test]
        public void PublicationClosureRetainsAbsorptionParametersAndRepeatContactPolicy()
        {
            var p=Example();var orb=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,"absorbing-orb")));
            Assert.IsTrue(orb.Content.Aoes.TryGet(6501,out var area));Assert.IsTrue(area.AbsorbProjectiles);Assert.AreEqual(.05f,area.ProjectileAbsorbForce);
            var repeat=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,"repeat-contact")));
            Assert.IsTrue(repeat.Content.Projectiles.TryGet(6502,out var projectile));Assert.AreEqual(.15f,projectile.SameTargetDelay);Assert.IsTrue(projectile.PassInvulnerable);
        }
        [Test]
        public void RepeatedReturnProjectileHitsTheSameTargetOnBothLegs()
        {
            var p=Example();p.Preview.Caster.X=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Hp=1000}};
            using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {Assert.IsTrue(simulation.Cast("repeat-contact",10,0).Accepted);simulation.Advance(80);Assert.AreEqual(2,simulation.Snapshot().Statistics.Hits);Assert.AreEqual(160,simulation.Snapshot().Statistics.Damage,.001);}
        }
    }
}
