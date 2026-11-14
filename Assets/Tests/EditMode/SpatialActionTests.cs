using System.IO;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SpatialActionTests
    {
        static SkillProjectDocument Example()
        { var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));p.Preview.Recovery=0;return p; }
        static SkillGraphDocument Skill(SkillProjectDocument p,string id)=>p.Skills.First(s=>s.Id==id);
        static SkillNodeDocument Node(SkillProjectDocument p,string id,string kind)=>Skill(p,id).Nodes.First(n=>n.Kind==kind);
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));

        [TestCase("melee-thrust",80,1)]
        [TestCase("melee-fan",160,2)]
        [TestCase("melee-whirl",160,2)]
        public void MeleeWaitsForStartupThenHitsEachVictimOncePerClip(string key,int damage,int hits)
        {
            using(var s=Simulation(Example()))
            {
                s.Cast(key,0,0);s.Advance(9);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);
                s.Advance(2);Assert.Greater(s.Snapshot().Volumes.Length,0);
                Assert.AreEqual(damage,s.Snapshot().Statistics.Damage);
                s.Advance(80);Assert.AreEqual(damage,s.Snapshot().Statistics.Damage);Assert.AreEqual(hits,s.Snapshot().Statistics.Hits);
                Assert.AreEqual(0,s.Snapshot().Volumes.Length);
            }
        }

        [Test]
        public void ThrustUsesItsWidthAndDirectionWhileCircleCanHitBehindTheCaster()
        {
            var p=Example();p.Preview.Targets=p.Preview.Targets.Take(2).ToArray();
            p.Preview.Targets[0].X=-6;p.Preview.Targets[0].Z=6;
            p.Preview.Targets[1].X=-10;p.Preview.Targets[1].Z=0;
            using(var s=Simulation(p))
            {s.Cast("melee-thrust",-6,6);s.Advance(80);Assert.AreEqual(920,s.Snapshot().Actors.First(a=>a.Name=="target-1").Hp);Assert.AreEqual(1000,s.Snapshot().Actors.First(a=>a.Name=="target-2").Hp);}
            using(var s=Simulation(p)){s.Cast("melee-whirl",0,0);s.Advance(80);Assert.AreEqual(160,s.Snapshot().Statistics.Damage);}
            using(var s=Simulation(p)){s.Cast("melee-fan",0,0);s.Advance(80);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);}
        }

        [Test]
        public void MeleeCanHitANewlyEnteringTargetAndRespectsItsTotalTargetBudget()
        {
            var p=Example();Node(p,"melee-fan","delivery").Parameters["maxTargets"]=1;
            using(var s=Simulation(p)){s.Cast("melee-fan",0,0);s.Advance(80);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);}
            p=Example();p.Preview.Targets=p.Preview.Targets.Take(1).ToArray();p.Preview.Targets[0].X=20;
            using(var s=Simulation(p))
            {s.Cast("melee-thrust",0,0);s.Advance(15);s.Place("target-1",0,0);s.Advance(2);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);s.Advance(30);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);}
        }

        [TestCase(20)]
        [TestCase(1000)]
        public void DashMovesItsExactDistanceAndSweepsTargetsEvenAcrossOneLargeStep(float speed)
        {
            var p=Example();Node(p,"dash-strike","delivery").Parameters["speed"]=speed;
            using(var s=Simulation(p))
            {
                s.Cast("dash-strike",0,0);s.Advance(80);
                Assert.AreEqual(6,s.Snapshot().Actors.First(a=>a.Kind=="caster").X,.002f);
                Assert.AreEqual(160,s.Snapshot().Statistics.Damage);Assert.AreEqual(2,s.Snapshot().Statistics.Hits);
                Assert.AreEqual(0,s.Snapshot().Volumes.Length);
                Assert.IsTrue(s.Snapshot().Trails.Any(t=>t.Entity==s.Snapshot().Actors.First(a=>a.Kind=="caster").Id));
            }
        }

        [Test]
        public void InterruptingDashStopsRemainingMotionAndClosingTheHitVolume()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("dash-strike",0,0);s.Advance(20);
                float stopped=s.Snapshot().Actors.First(a=>a.Kind=="caster").X;
                Assert.Greater(stopped,-6);Assert.Less(stopped,6);
                s.Interrupt();s.Advance(100);
                Assert.AreEqual(stopped,s.Snapshot().Actors.First(a=>a.Kind=="caster").X,.0001f);
                Assert.AreEqual(0,s.Snapshot().Volumes.Length);
            }
        }

        [Test]
        public void BlinkWaitsForItsDiscreteStartupThenLandsExactlyAndOnlyHitsAtDestination()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("blink-strike",0,0);s.Advance(9);
                Assert.AreEqual(-6,s.Snapshot().Actors.First(a=>a.Kind=="caster").X);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);
                s.Advance(2);Assert.AreEqual(0,s.Snapshot().Actors.First(a=>a.Kind=="caster").X,.0001f);
                Assert.AreEqual(240,s.Snapshot().Statistics.Damage);
                s.Advance(80);Assert.AreEqual(240,s.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void InvalidBlinkPointIsRejectedBeforeEnergyOrCooldownChanges()
        {
            using(var s=Simulation(Example()))
            {
                Assert.IsFalse(s.Cast("blink-strike",20,0).Accepted);
                Assert.AreEqual(100,s.Snapshot().Energy);Assert.AreEqual(0,s.Snapshot().Cooldowns["blink-strike"]);
                Assert.IsTrue(s.Cast("blink-strike",0,0).Accepted);
                s.Advance(6);s.Interrupt();s.Advance(100);
                Assert.AreEqual(-6,s.Snapshot().Actors.First(a=>a.Kind=="caster").X);
                Assert.AreEqual(0,s.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void FiniteBurstChargesBeforeEachNewActionAndHasIndependentHitDeduplication()
        {
            var p=Example();p.Preview.Energy=25;
            var cast=Node(p,"melee-thrust","cast").Parameters;cast["count"]=3;cast["interval"]=.25f;cast["costPerPulse"]=true;
            using(var s=Simulation(p))
            {s.Cast("melee-thrust",0,0);s.Advance(80);Assert.AreEqual(5,s.Snapshot().Energy);Assert.AreEqual(2,s.Snapshot().Statistics.Hits);Assert.AreEqual(160,s.Snapshot().Statistics.Damage);Assert.AreEqual(0,s.Snapshot().Volumes.Length);}
            p=Example();cast=Node(p,"melee-thrust","cast").Parameters;cast["count"]=3;
            using(var s=Simulation(p)){s.Cast("melee-thrust",0,0);s.Advance(80);Assert.AreEqual(240,s.Snapshot().Statistics.Damage);Assert.AreEqual(3,s.Snapshot().Statistics.Hits);}
        }

        [Test]
        public void ChannelReleaseAndChargedMeleeUseTheSameActionLifecycle()
        {
            var p=Example();var cast=Node(p,"melee-thrust","cast").Parameters;
            cast["mode"]="channel";cast["interval"]=.5f;cast["energyPerSecond"]=0;
            using(var s=Simulation(p))
            {s.Cast("melee-thrust",0,0);s.Advance(20);Assert.Greater(s.Snapshot().Volumes.Length,0);s.Release();Assert.AreEqual(0,s.Snapshot().Volumes.Length);int hits=s.Snapshot().Statistics.Hits;s.Advance(100);Assert.AreEqual(hits,s.Snapshot().Statistics.Hits);}
            cast["mode"]="charge";cast["autoRelease"]=false;
            using(var s=Simulation(p)){s.Cast("melee-thrust",0,0);s.Advance(30);s.Release();s.Advance(80);Assert.AreEqual(160,s.Snapshot().Statistics.Damage,.01f);}
        }

        [Test]
        public void PersistentEffectsCannotImplicitlyScheduleActorActionsAndOverlappingDashesAreRejected()
        {
            var p=Example();var cast=Node(p,"dash-strike","cast").Parameters;cast["count"]=2;
            StringAssert.Contains("overlaps",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
            p=Example();var skill=Skill(p,"melee-thrust");
            var projectile=SkillProjectJson.Read(SkillProjectJson.Write(p)).Skills.First(s=>s.Id=="basic-shot").Nodes.First(n=>n.Kind=="projectile");
            projectile.RuntimeId=7001;skill.Nodes=skill.Nodes.Concat(new[]{projectile}).ToArray();
            skill.Edges.First(e=>e.Source=="cast").Target="projectile";
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument {Id="projectile-action",Source="projectile",SourcePort="hit",Target="action",TargetPort="execute"}}).ToArray();
            StringAssert.Contains("Actor actions",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [Test]
        public void BlinkDoesNotHitAlongTheTravelPathAndRechecksALockedMovingTarget()
        {
            var p=Example();p.Preview.Targets=p.Preview.Targets.Take(2).ToArray();p.Preview.Targets[1].X=-3;p.Preview.Targets[1].Z=0;
            Node(p,"blink-strike","delivery").Parameters["hitRadius"]=.25f;
            using(var s=Simulation(p))
            {s.Cast("blink-strike",0,0);s.Advance(80);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);Assert.AreEqual(1000,s.Snapshot().Actors.First(a=>a.Name=="target-2").Hp);}
            p.Preview.Targets=p.Preview.Targets.Take(1).ToArray();Node(p,"blink-strike","target").Parameters["mode"]="nearest";
            using(var s=Simulation(p))
            {s.Cast("blink-strike",0,0);s.Advance(6);s.Place("target-1",20,0);s.Advance(80);Assert.AreEqual(-6,s.Snapshot().Actors.First(a=>a.Kind=="caster").X);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);}
        }
    }
}
