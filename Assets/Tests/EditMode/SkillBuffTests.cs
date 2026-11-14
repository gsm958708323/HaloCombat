using System;
using System.IO;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillBuffTests
    {
        static SkillProjectDocument Example()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillNodeDocument Node(SkillProjectDocument p,string skill,string kind)=>p.Skills.First(s=>s.Id==skill).Nodes.First(n=>n.Kind==kind);

        [Test]
        public void BurnProjectileAppliesATimedModifierAndDealsFourRealPeriods()
        {
            using(var s=Simulation(Example()))
            {
                Assert.IsTrue(s.Cast("burn-shot",0,0).Accepted);s.Advance(40);
                var target=s.Snapshot().Actors.First(a=>a.Name=="target-1");Assert.AreEqual(1,target.Buffs.Length);Assert.AreEqual("burn",target.Buffs[0].Id);
                Assert.AreEqual("caster",target.Buffs[0].Source);Assert.AreEqual(2.5f,target.Attributes["MoveSpeed"]);Assert.AreEqual(1,target.Tags[3101]);
                s.Advance(80);target=s.Snapshot().Actors.First(a=>a.Name=="target-1");
                Assert.AreEqual(960,target.Hp);Assert.AreEqual(40,s.Snapshot().Statistics.Damage);Assert.AreEqual(4,s.Snapshot().Statistics.Hits);
                Assert.AreEqual(0,target.Buffs.Length);Assert.AreEqual(5,target.Attributes["MoveSpeed"]);Assert.IsFalse(target.Tags.ContainsKey(3101));
                Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="buff-expired"));Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="buff-removed"));
            }
        }

        [Test]
        public void DispelSkillRemovesFutureDamageWithoutFiringNaturalExpiry()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("burn-shot",0,0);s.Advance(40);Assert.IsTrue(s.Cast("cleanse-burn",0,0).Accepted);s.Advance(100);
                Assert.AreEqual(10,s.Snapshot().Statistics.Damage);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="buff-expired"));
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="buff-removed"&&e.Result.Contains("Dispelled")));
            }
        }

        [Test]
        public void AreaPulsesStackOneSharedBuffWithoutDuplicatingTheModifier()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("burning-field",0,0);s.Advance(30);
                var targets=s.Snapshot().Actors.Where(a=>a.Name=="target-1"||a.Name=="target-2"||a.Name=="target-3").ToArray();
                foreach(var t in targets){Assert.AreEqual(1,t.Buffs.Length);Assert.AreEqual(3,t.Buffs[0].Stacks);Assert.AreEqual(2.5f,t.Attributes["MoveSpeed"]);}
                s.Advance(120);Assert.AreEqual(510,s.Snapshot().Statistics.Damage);foreach(var t in s.Snapshot().Actors)Assert.AreEqual(0,t.Buffs.Length);
            }
        }

        [Test]
        public void OverlappingAurasDispelOnlyTheirOwnSourceAndReleaseTheirOwnTagLeases()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("slow-aura",0,0);s.Advance(40);Assert.IsTrue(s.Cast("slow-aura",0,0).Accepted);s.Advance(10);
                var target=s.Snapshot().Actors.First(a=>a.Name=="target-1");Assert.AreEqual(2,target.Buffs.Length);Assert.AreNotEqual(target.Buffs[0].Source,target.Buffs[1].Source);
                Assert.AreEqual(1.25f,target.Attributes["MoveSpeed"]);Assert.AreEqual(2,target.Tags[3103]);
                s.Advance(10);target=s.Snapshot().Actors.First(a=>a.Name=="target-1");Assert.AreEqual(1,target.Buffs.Length);Assert.AreEqual(2.5f,target.Attributes["MoveSpeed"]);Assert.AreEqual(1,target.Tags[3103]);
                s.Advance(60);target=s.Snapshot().Actors.First(a=>a.Name=="target-1");Assert.AreEqual(0,target.Buffs.Length);Assert.AreEqual(5,target.Attributes["MoveSpeed"]);Assert.IsFalse(target.Tags.ContainsKey(3103));
            }
        }

        [Test]
        public void SelfBuffModifiesActualAttackAndOwnerCastHookThenRestoresAttributes()
        {
            var p=Example();var damage=Node(p,"basic-shot","damage").Parameters;damage["flat"]=0;damage["coefficient"]=1;damage["critChance"]=0;
            using(var s=Simulation(p))
            {
                s.Cast("battle-rage",0,0);s.Advance(20);var owner=s.Snapshot().Actors.First(a=>a.Kind=="caster");Assert.AreEqual(120,owner.Attributes["Atk"]);Assert.IsTrue(owner.Tags.ContainsKey(1004));
                Assert.IsTrue(s.Cast("basic-shot",0,0).Accepted);s.Advance(80);Assert.AreEqual(120,s.Snapshot().Statistics.Damage);
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="buff-cast"));Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="cue"&&e.Result=="61"));
                owner=s.Snapshot().Actors.First(a=>a.Kind=="caster");Assert.AreEqual(80,owner.Attributes["Atk"]);Assert.IsFalse(owner.Tags.ContainsKey(1004));
            }
        }

        [Test]
        public void ChargedBuffApplicationKeepsItsPowerAfterTheCastEnds()
        {
            var p=Example();var cast=Node(p,"burn-shot","cast").Parameters;cast["mode"]="charge";cast["autoRelease"]=false;cast["energyPerSecond"]=0;
            using(var s=Simulation(p)){s.Cast("burn-shot",0,0);s.Advance(30);s.Release();s.Advance(150);Assert.AreEqual(80,s.Snapshot().Statistics.Damage,.01f);}
        }

        [Test]
        public void InstanceRemovalAndIndependentWorldsDoNotMutateSharedBuffDefinitions()
        {
            var p=Example();var content=CombatContentCompiler.Compile(p);
            using(var first=new SkillSimulation(p,content))using(var second=new SkillSimulation(p,content))
            {
                first.Cast("burn-shot",0,0);second.Cast("burn-shot",0,0);first.Advance(40);second.Advance(40);
                var target=first.Snapshot().Actors.First(a=>a.Name=="target-1");Assert.IsTrue(first.RemoveBuff(target.Id,target.Buffs[0].InstanceId));Assert.IsFalse(first.RemoveBuff(target.Id,target.Buffs[0].InstanceId));
                first.Advance(100);second.Advance(100);Assert.AreEqual(10,first.Snapshot().Statistics.Damage);Assert.AreEqual(40,second.Snapshot().Statistics.Damage);
                content.Buffs.TryGet("burn",out var spec);Assert.AreEqual(1.1f,spec.Duration);Assert.AreEqual(.5f,spec.Modifiers[0].Value);
            }
        }
    }
}
