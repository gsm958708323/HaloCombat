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
    public sealed class ImpactEffectTests
    {
        static SkillProjectDocument Project(string mode)
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            p.Skills=new[]{p.Skills.First(s=>s.Id=="direct-hit")};p.SummonBlueprints=Array.Empty<SummonBlueprintDocument>();p.Buffs=Array.Empty<BuffDocument>();
            p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Hp=1000}};
            var skill=p.Skills[0];var damage=skill.Nodes.First(n=>n.Kind=="damage");damage.Kind="impact";
            damage.Parameters=JObject.FromObject(new{mode,duration=.3f,distance=2,speed=5,amount=100,recipient="target"});
            skill.Nodes.First(n=>n.Kind=="cast").Parameters["delay"]=0;
            return p;
        }
        [TestCase("hitstun",1007)]
        [TestCase("knockdown",1010)]
        [TestCase("iframe",1009)]
        public void StatusEffectsUseTheRealActivityAndHealthComponents(string mode,int tag)
        {
            var p=Project(mode);
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                s.Cast("direct-hit",2,0);s.Advance(2);Assert.IsTrue(s.Snapshot().Actors.First(a=>a.Name=="target").Tags.ContainsKey(tag));
                s.Advance(30);Assert.IsFalse(s.Snapshot().Actors.First(a=>a.Name=="target").Tags.ContainsKey(tag));
            }
        }
        [TestCase("knockback",4)]
        [TestCase("pull",0)]
        public void HitMovementUsesTheSharedMotorAndRequestedCenter(string mode,float expected)
        {
            var p=Project(mode);
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {s.Cast("direct-hit",2,0);s.Advance(3);Assert.AreEqual(expected,s.Snapshot().Actors.First(a=>a.Name=="target").X,.001f);}
        }
        [Test]
        public void LaunchAndShieldProduceRealHeightAndResourceState()
        {
            var p=Project("launch");
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {s.Cast("direct-hit",2,0);s.Advance(3);Assert.Greater(s.Snapshot().Actors.First(a=>a.Name=="target").Y,0);s.Advance(100);Assert.AreEqual(0,s.Snapshot().Actors.First(a=>a.Name=="target").Y,.001f);}
            p=Project("shield");
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {s.Cast("direct-hit",2,0);s.Advance(3);Assert.AreEqual(100,s.Snapshot().Actors.First(a=>a.Name=="target").Attributes["Shield"]);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="shield"));}
        }
    }
}
