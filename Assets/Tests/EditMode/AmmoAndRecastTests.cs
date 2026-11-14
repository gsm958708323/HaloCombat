using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class AmmoAndRecastTests
    {
        static SkillProjectDocument Example()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            p.Preview.Caster.X=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();p.Preview.Recovery=0;return p;
        }
        static SkillGraphDocument Graph(SkillProjectDocument p,string id)=>p.Skills.Single(s=>s.Id==id);
        static SkillNodeDocument Node(SkillProjectDocument p,string id,string kind)=>Graph(p,id).Nodes.First(n=>n.Kind==kind);
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillActorSnapshot Caster(SkillSimulation s)=>s.Snapshot().Actors.Single(a=>a.Kind=="caster");
        [Test]
        public void InsufficientAmmoRunsReloadsOwnTimelineAndRefillsAtOnePointOneSeconds()
        {
            var p=Example();p.Preview.InitialAmmo=0;
            using(var s=Simulation(p))
            {
                Assert.IsTrue(s.Cast("ammo-shot",20,0).Accepted);s.Advance(54);Assert.AreEqual(0,s.Snapshot().Ammo);Assert.AreEqual(0,s.Snapshot().Statistics.Projectiles);
                s.Advance(1);Assert.AreEqual(60,s.Snapshot().Ammo);s.Advance(3);Assert.IsTrue(s.Cast("ammo-shot",20,0).Accepted);Assert.AreEqual(59,s.Snapshot().Ammo);
                Assert.AreEqual(2,s.Snapshot().Statistics.Casts);s.Advance(5);Assert.AreEqual(1,s.Snapshot().Statistics.Projectiles);
            }
        }
        [Test]
        public void InterruptedReloadDoesNotRefillAndRefillNeverExceedsCapacity()
        {
            var p=Example();p.Preview.InitialAmmo=0;p.Preview.Ammo=5;
            using(var s=Simulation(p))
            {s.Cast("ammo-shot",20,0);s.Advance(20);s.Interrupt();s.Advance(100);Assert.AreEqual(0,s.Snapshot().Ammo);s.Cast("reload-ammo",20,0);s.Advance(55);Assert.AreEqual(5,s.Snapshot().Ammo);}
        }
        [Test]
        public void FallbackPaysItsOwnEnergyAndCanFailWithoutAnyPartialPayment()
        {
            var p=Example();p.Preview.InitialAmmo=0;p.Preview.InitialEnergy=3;Node(p,"reload-ammo","cast").Parameters["cost"]=5;
            using(var s=Simulation(p)){Assert.IsFalse(s.Cast("ammo-shot",20,0).Accepted);Assert.AreEqual(3,s.Snapshot().Energy);Assert.AreEqual(0,s.Snapshot().Ammo);Assert.AreEqual(0,s.Snapshot().Statistics.Casts);}
            p.Preview.InitialEnergy=6;Node(p,"ammo-shot","cast").Parameters["cost"]=99;
            using(var s=Simulation(p)){Assert.IsTrue(s.Cast("ammo-shot",20,0).Accepted);Assert.AreEqual(1,s.Snapshot().Energy);Assert.AreEqual(0,s.Snapshot().Ammo);}
        }
        [Test]
        public void RejectedTargetOrBusyCastCannotSpendAmmo()
        {
            var p=Example();Node(p,"ammo-shot","target").Parameters["mode"]="entity";
            using(var s=Simulation(p)){Assert.IsFalse(s.Cast("ammo-shot",20,0).Accepted);Assert.AreEqual(60,s.Snapshot().Ammo);}
            Node(p,"ammo-shot","target").Parameters["mode"]="facing";
            using(var s=Simulation(p)){Assert.IsTrue(s.Cast("ammo-shot",20,0).Accepted);Assert.IsFalse(s.Cast("ammo-shot",20,0).Accepted);Assert.AreEqual(59,s.Snapshot().Ammo);}
        }
        [TestCase(25,3,1,5,2)] [TestCase(100,1,1,80,0)] [TestCase(60,3,3,0,0)]
        public void BurstPulseChecksEnergyAndAmmoTogetherBeforeSpendingEither(int energy,int ammo,int shots,int energyLeft,int ammoLeft)
        {
            var p=Example();p.Preview.Energy=energy;p.Preview.Ammo=ammo;var graph=Graph(p,"ammo-shot");graph.Timeline=null;
            graph.Edges=graph.Edges.Concat(new[]{new SkillEdgeDocument{Id="cast-projectile",Source="cast",SourcePort="cast",Target="projectile",TargetPort="execute"}}).ToArray();
            var cast=Node(p,"ammo-shot","cast").Parameters;cast["fallbackSkill"]="";cast["cost"]=20;cast["costPerPulse"]=true;cast["count"]=3;cast["interval"]=.1f;cast["delay"]=0;cast["duration"]=.4f;
            using(var s=Simulation(p)){s.Cast("ammo-shot",20,0);s.Advance(25);Assert.AreEqual(shots,s.Snapshot().Statistics.Projectiles);Assert.AreEqual(energyLeft,s.Snapshot().Energy,.001);Assert.AreEqual(ammoLeft,s.Snapshot().Ammo);}
        }
        [Test]
        public void RecastTeleportsToTheActualProjectileAndConsumesItBeforeItCanMoveAgain()
        {
            using(var s=Simulation(Example()))
            {
                Assert.IsFalse(s.Cast("projectile-warp",20,0).Accepted);Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(26);
                var projectile=s.Snapshot().Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual("warp",projectile.TrackingKey);Assert.Greater(projectile.X,3);
                Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(1);Assert.AreEqual(projectile.X,Caster(s).X,.0001f);Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="projectile-teleport"));Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(5);Assert.AreEqual(1,s.Snapshot().Actors.Count(a=>a.Kind=="projectile"));
            }
        }
        [Test]
        public void BlockedLandingConsumesTheActionButKeepsTheTrackedProjectile()
        {
            var p=Example();p.Preview.Caster.CollisionRadius=.6f;p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=4,Z=0,Width=.2f,Depth=2}};
            using(var s=Simulation(p))
            {
                s.Cast("tracked-shot",20,0);s.Advance(26);string id=s.Snapshot().Actors.Single(a=>a.Kind=="projectile").Id;
                Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(1);Assert.AreEqual(0,Caster(s).X);Assert.IsTrue(s.Snapshot().Actors.Any(a=>a.Id==id));
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="projectile-teleport-blocked"));
            }
        }
        [Test]
        public void ProgressiveRampSamplesTheHistoricalRationalCurveOncePerFixedStep()
        {
            var p=Example();var projectile=Node(p,"tracked-shot","projectile").Parameters;projectile["trajectory"]="ramp";projectile["rampTime"]=5;
            using(var s=Simulation(p))
            {
                s.Cast("tracked-shot",20,0);s.Advance(5);Assert.AreEqual(.4f,s.Snapshot().Actors.Single(a=>a.Kind=="projectile").X,.00001f);
                s.Advance(21);float x=.4f,age=0,speed=0;
                for(int step=0;step<22;step++){speed=12*age/(age+5);x+=speed*.02f;age+=.02f;}
                var body=s.Snapshot().Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual(x,body.X,.00001f);Assert.AreEqual(speed,body.Speed.Value,.00001f);Assert.Less(body.X,1);
            }
        }
        [Test]
        public void ExpiredOrRemovedProjectileMakesTheNextPressFireAgainInsteadOfWarping()
        {
            var p=Example();Node(p,"tracked-shot","projectile").Parameters["lifetime"]=.1f;
            using(var s=Simulation(p)){s.Cast("tracked-shot",20,0);s.Advance(26);Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(5);Assert.AreEqual(2,s.Snapshot().Statistics.Projectiles);Assert.AreEqual(0,Caster(s).X);}
            using(var s=Simulation(Example())){s.Cast("tracked-shot",20,0);s.Advance(26);s.Remove(s.Snapshot().Actors.Single(a=>a.Kind=="projectile").Id);Assert.IsTrue(s.Cast("tracked-shot",20,0).Accepted);s.Advance(5);Assert.AreEqual(2,s.Snapshot().Statistics.Projectiles);}
        }
        [Test]
        public void CleanupFromAnOldGenerationCannotUntrackItsReplacement()
        {
            var loaded=new RuntimeProjectLoader(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));var world=loaded.CreateWorld(new FixedRandom(.5f));
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var owner);var tracker=owner.GetComp<TrackedProjectilesComp>();loaded.Content.Projectiles.TryGet(4004,out var definition);
                var old=world.SpawnActor(new ActorSpawnSpec("projectile"));world.TryGetActor(old,out var body);body.GetComp<ProjectileComp>().Setup(definition,owner.Id,0);tracker.Track("warp",old);
                world.RequestDespawn(old);world.FlushCleanup();var current=world.SpawnActor(new ActorSpawnSpec("projectile"));world.TryGetActor(current,out body);body.GetComp<ProjectileComp>().Setup(definition,owner.Id,0);tracker.Track("warp",current);
                Assert.AreEqual(old.Index,current.Index);Assert.AreNotEqual(old.Generation,current.Generation);tracker.ClearIf("warp",old);Assert.IsTrue(tracker.TryFind("warp",out var found));Assert.AreEqual(current,found.Id);
                world.RequestDespawn(current);world.FlushCleanup();Assert.IsFalse(tracker.TryFind("warp",out _));
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void PublicationsIncludeReloadAndRecastDependenciesAndBothSlotsShareAmmo()
        {
            var p=Example();p.Preview.Ammo=2;var ammo=SkillPublicationJson.Create(p,"ammo-shot");var tracked=SkillPublicationJson.Create(p,"tracked-shot");
            CollectionAssert.AreEquivalent(new[]{"ammo-shot","reload-ammo"},ammo.Project.Skills.Select(s=>s.Id));CollectionAssert.AreEquivalent(new[]{"tracked-shot","projectile-warp"},tracked.Project.Skills.Select(s=>s.Id));
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(ammo)},{"f7",SkillPublicationJson.Write(tracked)}});var s=session.Simulation;
                Assert.IsTrue(s.Cast(session.Slots["f6"],20,0).Accepted);s.Advance(40);Assert.AreEqual(1,s.Snapshot().Ammo);Assert.IsTrue(s.Cast(session.Slots["f7"],20,0).Accepted);s.Advance(26);
                Assert.IsTrue(s.Cast(session.Slots["f7"],20,0).Accepted);s.Advance(1);Assert.AreEqual(1,s.Snapshot().Ammo);Assert.Greater(Caster(s).X,3);
            }
        }
        [Test]
        public void FormalPrimaryInputConsumesRealAmmoAndFallsBackToTheConfiguredReload()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            p.Runtime.Actors.Single(a=>a.Id=="arena-player").InitialAmmo=1;
            using(var s=new ArenaProject(p).Create(true))
            {
                s.Start();s.ApplyInput(new ArenaInputFrame{PrimaryHeld=true,Pressed=new[]{"Fire1"}});s.Advance(41);s.World.TryGetActor(s.PlayerId,out var player);
                Assert.AreEqual(0,player.GetComp<AmmoComp>().Current);Assert.AreEqual(SkillNodeId.None,player.GetComp<SkillDirectorComp>().CurrentSkill);
                // The migrated Light primary now explicitly stops its source for one
                // step. The finished action is observed by the driver on step42.
                s.Advance();Assert.AreEqual(new SkillNodeId(2009),player.GetComp<SkillDirectorComp>().CurrentSkill);
                s.Advance(54);Assert.AreEqual(60,player.GetComp<AmmoComp>().Current);
            }
        }
        [TestCase("missing")] [TestCase("cycle")] [TestCase("no-cost")] [TestCase("unpaired")] [TestCase("fraction")] [TestCase("initial")]
        public void InvalidResourceAndRoutingDefinitionsAreRejectedBeforeTheyCanPublish(string invalid)
        {
            var p=Example();var cast=Node(p,"ammo-shot","cast").Parameters;
            if(invalid=="missing")cast["fallbackSkill"]="missing";
            if(invalid=="cycle"){var reload=Node(p,"reload-ammo","cast").Parameters;reload["ammoCost"]=1;reload["fallbackSkill"]="ammo-shot";}
            if(invalid=="no-cost")cast["ammoCost"]=0;
            if(invalid=="unpaired")Node(p,"tracked-shot","cast").Parameters["recastProjectile"]="";
            if(invalid=="fraction")cast["ammoCost"]=.5f;
            if(invalid=="initial")p.Preview.InitialAmmo=61;
            Assert.Throws<ContentValidationException>(()=>SkillPublicationJson.Create(p,"ammo-shot"));
        }
    }
}
