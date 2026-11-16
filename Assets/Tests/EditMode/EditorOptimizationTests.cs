using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class EditorOptimizationTests
    {
        static SkillProjectDocument Project()
        {
            var p=SkillProjectJson.SelectTemplate(SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")),"basic-shot");
            p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=3,Hp=1000}};
            Node(p,"cast").Parameters=JObject.FromObject(new{cost=0,cooldown=0,delay=0,duration=.3});
            Node(p,"projectile").Parameters=JObject.FromObject(new{speed=1,range=24,radius=.1,spawnForward=0});
            Node(p,"damage").Parameters=JObject.FromObject(new{flat=10,coefficient=0,critChance=0});return p;
        }
        static SkillNodeDocument Node(SkillProjectDocument p,string kind)=>p.Skills[0].Nodes.Single(n=>n.Kind==kind);
        [Test]
        public void LiveEditsPreserveWorldAndOldProjectileWhileFutureCastsUseTheNewDefinition()
        {
            var p=Project();using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p),true))
            {
                sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(5);var before=sim.Snapshot();
                var updated=SkillProjectJson.Read(SkillProjectJson.Write(p));Node(updated,"damage").Parameters["flat"]=25;
                sim.Execute(new SkillSimulationCommand{Kind="configure",Project=updated});var after=sim.Snapshot();
                Assert.AreEqual(before.Step,after.Step);CollectionAssert.AreEqual(before.Actors.Select(a=>(a.Id,a.X,a.Z,a.Hp)),after.Actors.Select(a=>(a.Id,a.X,a.Z,a.Hp)));
                sim.Advance(150);Assert.AreEqual(10,sim.Snapshot().Statistics.Damage);
                sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(150);Assert.AreEqual(35,sim.Snapshot().Statistics.Damage);
                using(var replay=new SkillReplayRun(sim.CaptureReplay("basic-shot")))Assert.IsTrue(replay.Comparison.Matches);
                before=sim.Snapshot();Node(updated,"damage").Parameters["flat"]=-1;
                Assert.Throws<ContentValidationException>(()=>sim.ApplyConfiguration(updated));
                Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(before),JToken.FromObject(sim.Snapshot())));
            }
        }
        [Test]
        public void OrdinaryProjectUsesAutoCancelWindowAndUpdatedInputBufferWithoutAnArena()
        {
            var p=Project();p.Runtime=new RuntimeContentDocument();
            p.Runtime.Combos=new[]{new RuntimeComboDocument{Id="root",Input="Attack",ToSkill="basic-shot"},
                new RuntimeComboDocument{Id="loop",Input="Attack",ToSkill="basic-shot",PreSkills=new[]{"basic-shot"},Priority=10}};
            Node(p,"cast").Parameters["cancelStart"]=.12;Node(p,"cast").Parameters["cancelEnd"]=.3;
            p.Preview.InputBufferSeconds=.02f;
            using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                sim.Input("Attack",10,0);sim.Advance(2);Assert.AreEqual(1,sim.Snapshot().Statistics.Casts);
                p.Preview.InputBufferSeconds=.3f;sim.ApplyConfiguration(p);sim.Input("Attack",10,0);sim.Advance(8);
                Assert.AreEqual(2,sim.Snapshot().Statistics.Casts);Assert.IsTrue(sim.Snapshot().Events.Any(e=>e.Kind=="combo-resolved"&&e.Result.Contains("loop")));
            }
        }
        [Test]
        public void CompileDoesNotRewriteCallerAndLegacyUpgradeIsIdempotent()
        {
            var p=Project();Node(p,"projectile").Parameters["maxHits"]=2;string before=SkillProjectJson.Write(p);
            CombatContentCompiler.Compile(p);Assert.AreEqual(before,SkillProjectJson.Write(p));
            var canonical=SkillProjectJson.Read(before);Assert.AreEqual(1,canonical.Skills[0].Nodes.Count(n=>n.Kind=="pierce"));
            string once=SkillProjectJson.Write(canonical);Assert.AreEqual(once,SkillProjectJson.Write(SkillProjectJson.Read(once)));
        }
        [Test]
        public void DashStopsAtAnAlliedBodyWithoutDeliveringHostileContactDamage()
        {
            var p=Project();p.Preview.Targets[0].Team=1;
            var action=Node(p,"projectile");action.Kind="delivery";action.Parameters=JObject.FromObject(new{mode="displacement",movementMode="dash",distance=6,speed=20,hitRadius=.2,startup=0});
            p.Skills[0].Edges.Single(e=>e.Source==action.Id).SourcePort="apply";
            using(var sim=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                sim.Cast("basic-shot",10,0);sim.Advance(80);var state=sim.Snapshot();
                Assert.AreEqual(0,state.Statistics.Hits);Assert.Less(state.Actors.Single(a=>a.Kind=="caster").X,2.21f);
            }
        }
    }
}
