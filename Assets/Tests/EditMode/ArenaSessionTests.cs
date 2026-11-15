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
    public sealed class ArenaSessionTests
    {
        static SkillProjectDocument Project()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static ArenaSession Session(bool training=false,SkillProjectDocument p=null)=>new ArenaProject(p??Project()).Create(training);
        static Actor Player(ArenaSession s){s.World.TryGetActor(s.PlayerId,out var actor);return actor;}
        static ArenaInputFrame Input(bool held=false,params string[] keys)=>new ArenaInputFrame{PrimaryHeld=held,Pressed=keys,AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)};
        static void Kill(ArenaSession s,Actor a){a.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="test"});}

        [Test]
        public void SessionStartPublishesConfiguredPlayerAfterPresentationCanSubscribe()
        {
            using(var s=Session())
            {
                Assert.AreEqual(0,s.World.RegistryActive().Count);bool configured=false;
                s.World.Events.Subscribe<EvEntitySpawn>(e=>{s.World.TryGetActor(e.Id,out var a);configured=e.BlueprintId=="arena-player"&&a.GetComp<AttributeSet>().GetBase(AttrId.Hp)==100000;});
                s.Start();Assert.IsTrue(configured);Assert.AreEqual(0,s.EnemyCount);s.Advance();Assert.AreEqual(10,s.EnemyCount);
                Assert.AreEqual(10,s.SpawnedEnemies);Assert.Throws<InvalidOperationException>(()=>s.Start());
            }
        }
        [Test]
        public void TrainingModeSpawnsExactlyOneStationaryTargetAndNeverAddsWaves()
        {
            using(var s=Session(true))
            {
                s.Start();s.Advance(1000);var targets=s.World.RegistryActive().Where(a=>a.Id!=s.PlayerId).ToArray();Assert.AreEqual(1,targets.Length);
                Assert.IsFalse(targets[0].TryGetComp<BehaviorTreeComp>(out _));Assert.AreEqual(0,s.SpawnedEnemies);
                Assert.AreEqual(4,targets[0].GetComp<TransformComp>().Position.X,.0001f);
            }
        }
        [Test]
        public void SameSeedAndInputProduceIdenticalSpawnsMovementAndSkillEvents()
        {
            using(var a=Session())using(var b=Session())
            {
                a.Start();b.Start();var input=Input(true,"Fire1");input.MoveZ=.2f;a.ApplyInput(input);b.ApplyInput(input);a.Advance(35);b.Advance(35);
                var av=a.World.RegistryActive();var bv=b.World.RegistryActive();Assert.AreEqual(av.Count,bv.Count);
                for(int i=0;i<av.Count;i++){Assert.AreEqual(av[i].Id,bv[i].Id);Assert.AreEqual(av[i].GetComp<TransformComp>().Position.X,bv[i].GetComp<TransformComp>().Position.X);Assert.AreEqual(av[i].GetComp<TransformComp>().Position.Z,bv[i].GetComp<TransformComp>().Position.Z);}
            }
        }
        [Test]
        public void PressSurvivesRenderFramesWithNoLogicStepAndIsConsumedOnce()
        {
            using(var s=Session(true))
            {s.Start();int casts=0;s.World.Events.Subscribe<EvSkillStarted>(e=>casts++);s.ApplyInput(Input(false,"Fire1"));s.ApplyInput(Input());s.Advance();Assert.AreEqual(1,casts);s.Advance(10);Assert.AreEqual(1,casts);}
        }
        [Test]
        public void ExplicitPressTakesPriorityOverHeldPrimary()
        {
            using(var s=Session(true))
            {s.Start();s.ApplyInput(Input(true,"Homing"));s.Advance();Assert.AreEqual(new SkillNodeId(2004),Player(s).GetComp<SkillDirectorComp>().CurrentSkill);}
        }
        [Test]
        public void MultiplePressesUseHistoricalPriorityAndDoNotOverwriteTheFirstCommand()
        {
            using(var s=Session(true))
            {s.Start();s.ApplyInput(Input(false,"Fire1","Roll","Homing"));s.Advance();Assert.AreEqual(new SkillNodeId(2002),Player(s).GetComp<SkillDirectorComp>().CurrentSkill);}
        }
        [Test]
        public void AimKeepsMouseDirectionWhileWalkingAnotherWay()
        {
            using(var s=Session(true))
            {s.Start();var input=Input();input.MoveZ=1;s.ApplyInput(input);s.Advance(10);var tf=Player(s).GetComp<TransformComp>();Assert.Greater(tf.Position.Z,0);Assert.AreEqual(0,tf.YawDegrees,.0001f);}
        }
        [Test]
        public void MouseAimCannotOverrideTheLockedFacingOfKnockdown()
        {
            using(var s=Session(true))
            {
                s.Start();var actor=Player(s);actor.GetComp<StateMachineComp>().TryEnter(ActivityId.Knockdown,new ActivityEnterArgs{Reason="test"});
                var input=Input();input.Aim=new SkillAim(EntityId.Invalid,new SimVec3(0,0,4),90);s.ApplyInput(input);s.Advance(2);
                Assert.AreEqual(ActivityId.Knockdown,actor.GetComp<StateMachineComp>().Current);Assert.AreEqual(0,actor.GetComp<TransformComp>().YawDegrees,.001f);
            }
        }
        [Test]
        public void PauseFreezesGameplayAndWallClockAndClearsHeldInput()
        {
            using(var s=Session(true))
            {s.Start();s.ApplyInput(Input(true,"Fire1"));s.Advance(5);int frame=s.World.Time.Frame;s.SetPaused(true);s.Advance(100);s.AdvanceWall(20);Assert.AreEqual(frame,s.World.Time.Frame);Assert.IsFalse(Player(s).GetComp<PlayerAimComp>().PrimaryHeld);s.SetPaused(false);s.Advance();Assert.AreEqual(frame+1,s.World.Time.Frame);}
        }
        [Test]
        public void DeathStopsCombatButCorpsesAreRemovedUsingWallTimeWithoutAnExtraWorldTick()
        {
            using(var s=Session())
            {
                s.Start();s.Advance();var enemy=s.World.RegistryActive().First(a=>a.Id!=s.PlayerId);Kill(s,enemy);Kill(s,Player(s));Assert.IsTrue(s.PlayerDead);
                int frame=s.World.Time.Frame;s.Advance(500);Assert.AreEqual(frame,s.World.Time.Frame);s.AdvanceWall(4.99f);Assert.IsTrue(s.World.TryGetActor(s.PlayerId,out _));
                s.AdvanceWall(.02f);Assert.IsFalse(s.World.TryGetActor(s.PlayerId,out _));Assert.IsFalse(s.World.TryGetActor(enemy.Id,out _));Assert.AreEqual(frame,s.World.Time.Frame);
            }
        }
        [Test]
        public void WaveRefillRespectsLivingLimitAndWaitsForItsPeriod()
        {
            var p=Project();p.Runtime.Arena.SpawnPeriod=.1f;
            using(var s=Session(false,p))
            {s.Start();s.Advance();var enemies=s.World.RegistryActive().Where(a=>a.Id!=s.PlayerId).Take(2).ToArray();foreach(var enemy in enemies)Kill(s,enemy);Assert.AreEqual(8,s.EnemyCount);s.Advance(4);Assert.AreEqual(8,s.EnemyCount);s.Advance();Assert.AreEqual(10,s.EnemyCount);Assert.AreEqual(12,s.SpawnedEnemies);}
        }
        [Test]
        public void HostileProjectilesAndAreasDoNotCountAsLivingEnemies()
        {
            using(var s=Session())
            {
                s.Start();s.Advance();
                foreach(string blueprint in new[]{"projectile","aoe"})
                {s.World.TryGetActor(s.World.SpawnActor(new ActorSpawnSpec(blueprint)),out var body);body.GetComp<TeamComp>().SetTeam(2);}
                Assert.AreEqual(10,s.EnemyCount);
            }
        }
        [Test]
        public void ArenaBoundaryUsesCoreCollisionForMovement()
        {
            using(var s=Session(true))
            {s.Start();var input=Input();input.MoveZ=1;s.ApplyInput(input);s.Advance(1000);Assert.LessOrEqual(Player(s).GetComp<TransformComp>().Position.Z,s.Definition.HalfDepth-.25f+.001f);}
        }
        [Test]
        public void DisposeClearsPendingIntentsActorsAndRejectsFurtherInputs()
        {
            var s=Session();s.Start();var player=Player(s);s.ApplyInput(Input(true,"Fire1"));s.Advance(10);s.Dispose();s.Dispose();s.ApplyInput(Input(true,"Fire1"));s.Advance(10);
            Assert.IsFalse(player.IsActive);Assert.AreEqual(0,s.World.RegistryActive().Count);Assert.AreEqual(0,s.World.Intents.Count<SpawnProjectileIntent>());
        }
        [TestCase("player")] [TestCase("bounds")] [TestCase("binding")] [TestCase("spawn")]
        public void InvalidArenaSettingsAreRejectedBeforeSessionCreation(string field)
        {
            var p=Project();if(field=="player")p.Runtime.Arena.Player="missing";
            if(field=="bounds")p.Runtime.Arena.HalfWidth=float.NaN;
            if(field=="binding")p.Runtime.Arena.Bindings[0].Input="unknown";
            if(field=="spawn")p.Runtime.Arena.PlayerX=p.Runtime.Arena.HalfWidth;
            Assert.Throws<ContentValidationException>(()=>new ArenaProject(p));
        }
        [Test]
        public void PublishedSkillOverridesFormalDefinitionAndRunsThroughTheSamePlayerGuard()
        {
            var p=Project();var edited=Project();edited.Skills[0].Nodes.First(n=>n.Kind=="cast").Parameters["cost"]=20;
            var publications=new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(edited,"g1"))}};
            using(var s=new ArenaProject(p,publications).Create(true))
            {s.Start();s.ApplyInput(Input());Assert.IsTrue(s.CastSlot("f6"));Assert.AreEqual(80,Player(s).GetComp<EnergyComp>().Current);Assert.IsFalse(s.CastSlot("f6"));Assert.AreEqual(80,Player(s).GetComp<EnergyComp>().Current);}
        }
        [Test]
        public void WrongProjectOrConflictingSlotVersionsCannotReplaceContent()
        {
            var p=Project();var other=Project();other.Id="different";var files=new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(other,"g1"))}};
            Assert.Throws<ContentValidationException>(()=>new ArenaProject(p,files));
            other=Project();files["f6"]=SkillPublicationJson.Write(SkillPublicationJson.Create(other,"g1"));other.Skills[0].Name="conflict";files["f7"]=SkillPublicationJson.Write(SkillPublicationJson.Create(other,"g1"));
            Assert.Throws<ContentValidationException>(()=>new ArenaProject(p,files));
        }
        static SkillProjectDocument HeldProject(bool charge=false)
        {
            var p=Project();var examples=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            var graph=examples.Skills.First(s=>s.Id==(charge?"charged-shot":"basic-shot"));graph.Id="g1";graph.RuntimeId=101;p.Skills[0]=graph;
            p.Runtime.Arena.Bindings.First(b=>b.Action=="Fire1").Input="Attack";
            // Real Arena definitions now own 4001/4004; this synthetic held-cast fixture
            // needs its own definition identities while preserving the formal skill ID.
            int definition=p.Skills.Skip(1).SelectMany(s=>s.Nodes).Select(n=>n.RuntimeId).Concat(p.Buffs.SelectMany(b=>b.Nodes).Select(n=>n.RuntimeId)).Max();
            foreach(var n in graph.Nodes)if(n.Kind=="projectile"||n.Kind=="delivery")n.RuntimeId=++definition;
            var cast=graph.Nodes.First(n=>n.Kind=="cast").Parameters;
            cast["mode"]=charge?"charge":"channel";cast["autoRelease"]=false;cast["sustained"]=true;cast["interval"]=.1f;cast["delay"]=0;cast["cost"]=0;cast["energyPerSecond"]=0;
            return p;
        }
        [Test]
        public void HeldPrimaryStopsPulsingOnItsOwnRelease()
        {
            using(var s=Session(true,HeldProject()))
            {
                s.Start();int spawned=0;s.World.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId=="projectile")spawned++;});
                s.ApplyInput(Input(true,"Fire1"));s.Advance(15);Assert.IsTrue(Player(s).GetComp<SkillCastComp>().IsChanneling);Assert.GreaterOrEqual(spawned,3);
                s.ApplyInput(Input());int count=spawned;s.Advance(30);Assert.IsFalse(Player(s).GetComp<SkillCastComp>().IsChanneling);Assert.AreEqual(count,spawned);
            }
        }
        [Test]
        public void HeldPrimaryLetsInstantTimelineFinishBeforeRepeating()
        {
            using(var s=Session(true))
            {
                s.Start();float damage=0;int combos=0;s.World.Events.Subscribe<EvDamage>(e=>damage+=e.Amount);
                s.World.Events.Subscribe<EvSkillStarted>(e=>{if(e.Skill==SkillNodeId.G2)combos++;});
                s.ApplyInput(Input(true,"Fire1"));s.Advance(90);Assert.Greater(damage,0);Assert.AreEqual(0,combos,"Held repeat must not synthesize new Combo presses.");
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void PausingHeldCastCancelsWithoutReleasingCharge(bool charge)
        {
            using(var s=Session(true,HeldProject(charge)))
            {
                s.Start();int released=0;s.World.Events.Subscribe<EvSkillReleased>(e=>released++);
                s.ApplyInput(Input(true,"Fire1"));s.Advance(20);var cast=Player(s).GetComp<SkillCastComp>();Assert.IsTrue(cast.IsChanneling||cast.IsCharging);
                s.SetPaused(true);s.SetPaused(false);s.Advance(50);Assert.IsFalse(cast.IsChanneling||cast.IsCharging);Assert.AreEqual(0,released);
            }
        }
        [Test]
        public void FailedSlotAndPrimaryReleasesCannotStopAnotherSlotsHeldCast()
        {
            var p=HeldProject();var publications=new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"g1"))},{"f7",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"homing"))}};
            using(var s=new ArenaProject(p,publications).Create(true))
            {
                s.Start();s.ApplyInput(Input(true));Assert.IsTrue(s.CastSlot("f6"));s.Advance(10);Assert.IsFalse(s.CastSlot("f7"));
                s.ReleaseSlot("f7");s.ApplyInput(Input());Assert.IsTrue(Player(s).GetComp<SkillCastComp>().IsChanneling);
                s.ReleaseSlot("f6");Assert.IsFalse(Player(s).GetComp<SkillCastComp>().IsChanneling);
            }
        }
        [Test]
        public void InvalidAimHeightIsRejectedWithoutAdvancingTheWorld()
        {
            using(var s=Session(true))
            {s.Start();var input=Input();input.Aim=new SkillAim(EntityId.Invalid,new SimVec3(0,float.NaN,0),0);Assert.Throws<ArgumentException>(()=>s.ApplyInput(input));Assert.AreEqual(0,s.World.Time.Frame);}
        }
        [Test]
        public void TrainingSpawnNearBoundaryFallsBackToAnInteriorFreePosition()
        {
            var p=Project();p.Runtime.Arena.PlayerX=15;
            using(var s=Session(true,p))
            {s.Start();s.Advance();var dummy=s.World.RegistryActive().First(a=>a.Id!=s.PlayerId);var point=dummy.GetComp<TransformComp>().Position;Assert.LessOrEqual(Math.Abs(point.X)+.25f,s.Definition.HalfWidth);Assert.IsTrue(s.World.Collision.IsFree(point,.25f));}
        }
    }
}
