using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ConstructTests
    {
        static SkillProjectDocument Example()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=0,Z=0,Hp=100000,Atk=0}};
            Cast(p).Parameters["delay"]=0;Node(p).Parameters["decayPeriod"]=0;
            p.Skills.Single(s=>s.Id=="barrel-chain").Nodes.Single(n=>n.Kind=="damage").Parameters["canCrit"]=false;
            return p;
        }
        static SkillNodeDocument Node(SkillProjectDocument p)=>p.Skills.Single(s=>s.Id=="barrel-chain").Nodes.Single(n=>n.Kind=="construct");
        static SkillNodeDocument Cast(SkillProjectDocument p)=>p.Skills.Single(s=>s.Id=="barrel-chain").Nodes.Single(n=>n.Kind=="cast");
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillActorSnapshot[] Bodies(SkillSimulation s)=>s.Snapshot().Actors.Where(a=>a.Kind=="construct").ToArray();
        static CombatWorld World(out RuntimeProjectLoader loaded)
        {loaded=new RuntimeProjectLoader(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));return loaded.CreateWorld(new FixedRandom(.9f));}
        static Actor Spawn(CombatWorld world,string id,float x=0)
        {world.TryGetActor(world.SpawnActor(new ActorSpawnSpec(id)),out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,0);return actor;}
        static Actor Body(CombatWorld world,Actor owner,ConstructDefinition definition,float x=0)
        {var a=Spawn(world,"construct",x);a.GetComp<ConstructComp>().Setup(definition,owner?.Id??EntityId.Invalid,1,default);return a;}
        static void Damage(CombatWorld world,Actor source,Actor target,float amount)
            =>world.Deliver(new IEffect[]{new DamageEffect{Flat=amount,Coeff=0,CanCrit=false,IgnoreDef=true}},source,target,0);
        static float Hp(Actor actor)=>actor.GetComp<AttributeSet>().GetBase(AttrId.Hp);
        [Test]
        public void SpawnEventContainsConfiguredHealthPositionOwnerAndView()
        {
            var world=World(out var loaded);
            try
            {
                var player=Spawn(world,"arena-player");bool configured=false;
                var node=loaded.Project.Skills.Single(s=>s.Id=="arena-barrel").Nodes.Single(n=>n.Kind=="construct");
                string view=node.Parameters.Value<string>("view");Assert.AreEqual("buff_barrel_view",view);
                world.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId!="construct")return;world.TryGetActor(e.Id,out var a);configured=Hp(a)==5&&e.Owner==player.Id&&e.ViewKey==view&&Math.Abs(a.GetComp<TransformComp>().Position.X-.55f)<.0001f;});
                Assert.IsTrue(player.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2008),new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)));
                for(int i=0;i<5;i++)world.Tick(.02f);Assert.IsTrue(configured);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void DecayUsesLogicalAgeAndExactBoundaryWithoutADeathOnManualRemoval()
        {
            var p=Example();Node(p).Parameters["count"]=1;Node(p).Parameters["hp"]=3;Node(p).Parameters["decayPeriod"]=.1f;
            using(var s=Simulation(p))
            {
                s.Cast("barrel-chain",0,0);s.Advance(1);s.Advance(4);Assert.AreEqual(3,Bodies(s).Single().Hp);s.Advance(1);Assert.AreEqual(2,Bodies(s).Single().Hp);
                s.RequestHitstop(10);s.Advance(10);Assert.AreEqual(2,Bodies(s).Single().Hp);s.Advance(5);Assert.AreEqual(1,Bodies(s).Single().Hp);
                s.Remove(Bodies(s).Single().Id);s.Advance(1);Assert.AreEqual(0,Bodies(s).Length);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="construct-died"));
            }
        }
        [Test]
        public void PeriodicSelfDamageKillsExactlyOnceAndSpillTimeIsRetained()
        {
            var world=World(out _);
            try
            {
                int deaths=0;world.Events.Subscribe<EvConstructLifecycle>(e=>{if(e.Kind==ConstructEventKind.Died)deaths++;});
                var body=Body(world,null,new ConstructDefinition{SpecId=999,Group="test",Hp=3,DecayPeriod=.1f});world.Tick(.25f);Assert.AreEqual(1,Hp(body));
                world.Tick(.05f);Assert.AreEqual(1,deaths);Assert.AreEqual(0,world.RegistryActive().Count);world.Tick(1);Assert.AreEqual(1,deaths);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void IncomingCapAppliesBeforeShieldAndSameGroupDamageBypassesIt()
        {
            var world=World(out _);
            try
            {
                var source=Spawn(world,"arena-player");var body=Body(world,source,new ConstructDefinition{SpecId=1,Group="powder",Hp=5,DecayPeriod=0});
                body.GetComp<AttributeSet>().SetBase(AttrId.Shield,2);Damage(world,source,body,80);Assert.AreEqual(5,Hp(body));Assert.AreEqual(1,body.GetComp<AttributeSet>().GetBase(AttrId.Shield));
                Damage(world,source,body,80);Damage(world,source,body,80);Assert.AreEqual(4,Hp(body));
                var same=Body(world,source,new ConstructDefinition{SpecId=2,Group="powder",DecayPeriod=0});Damage(world,same,body,9999);Assert.IsFalse(body.IsActive);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void DifferentGroupsCannotBypassAnObjectsIncomingCap()
        {
            var world=World(out _);
            try{var a=Body(world,null,new ConstructDefinition{SpecId=1,Group="a",DecayPeriod=0});var b=Body(world,null,new ConstructDefinition{SpecId=2,Group="b",DecayPeriod=0});Damage(world,a,b,9999);Assert.AreEqual(4,Hp(b));}
            finally{world.Shutdown();}
        }
        [Test]
        public void ThreeObjectsChainOnceAndAllBlastsUseTheSameRealEnemyFilter()
        {
            using(var s=Simulation(Example()))
            {
                s.Cast("barrel-chain",0,0);s.Advance(25);Assert.AreEqual(3,Bodies(s).Length);Assert.IsTrue(s.Cast("detonate-constructs",0,0).Accepted);s.Advance(1);
                var result=s.Snapshot();Assert.AreEqual(0,Bodies(s).Length);Assert.AreEqual(99964,result.Actors.Single(a=>a.Kind=="target").Hp);Assert.AreEqual(1000,result.Actors.Single(a=>a.Kind=="caster").Hp);
                Assert.AreEqual(3,result.Events.Count(e=>e.Kind=="construct-died"));Assert.AreEqual(51,result.Statistics.Damage);Assert.AreEqual(6,result.Statistics.Hits);
            }
        }
        [Test]
        public void DeathEffectsReadTheOwnersCurrentAttackRatherThanItsSpawnSnapshot()
        {
            var world=World(out var loaded);
            try
            {
                var owner=Spawn(world,"arena-player",-6);var target=Spawn(world,"arena-target",1);loaded.Content.Constructs.TryGet(5001,out var definition);
                target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);
                var body=Body(world,owner,definition);owner.GetComp<AttributeSet>().SetBase(AttrId.Atk,200);float before=Hp(target);Damage(world,body,body,9999);Assert.AreEqual(30,before-Hp(target),.001f);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void ReusedOwnerIndexCannotDonateANewActorsAttackToOldObjects()
        {
            var world=World(out var loaded);
            try
            {
                var owner=Spawn(world,"arena-player",-6);var id=owner.Id;loaded.Content.Constructs.TryGet(5001,out var definition);var body=Body(world,owner,definition);var target=Spawn(world,"arena-target",1);
                world.RequestDespawn(id);world.FlushCleanup();var replacement=Spawn(world,"arena-player",-6);Assert.AreEqual(id.Index,replacement.Id.Index);Assert.AreNotEqual(id.Generation,replacement.Id.Generation);
                replacement.GetComp<AttributeSet>().SetBase(AttrId.Atk,10000);float before=Hp(target);Damage(world,body,body,9999);Assert.AreEqual(before,Hp(target));
            }
            finally{world.Shutdown();}
        }
        [TestCase(false)] [TestCase(true)]
        public void OwnerCleanupIsExplicitAndNeverInvokesDeathEffects(bool cleanup)
        {
            var world=World(out _);
            try
            {
                var owner=Spawn(world,"arena-player");int deaths=0,removed=0;var definition=new ConstructDefinition{SpecId=1,Group="test",CleanupWithOwner=cleanup,OnDeath=new IEffect[]{new CallbackEffect(()=>deaths++)},OnRemoved=new IEffect[]{new CallbackEffect(()=>removed++)}};
                var body=Body(world,owner,definition);world.RequestDespawn(owner.Id);world.FlushCleanup();Assert.AreEqual(!cleanup,body.IsActive);Assert.AreEqual(0,deaths);Assert.AreEqual(cleanup?1:0,removed);
                world.Shutdown();Assert.AreEqual(0,deaths);Assert.AreEqual(cleanup?1:0,removed);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void BadSpawnAndOwnerLimitsPublishRejectionsWithoutRefundingTheSuccessfulCast()
        {
            var p=Example();Node(p).Parameters["count"]=1;Node(p).Parameters["maxAlive"]=1;Cast(p).Parameters["cost"]=10;p.Preview.Recovery=0;
            using(var s=Simulation(p)){s.Cast("barrel-chain",0,0);s.Advance(25);s.Cast("barrel-chain",0,0);s.Advance(1);Assert.AreEqual(1,Bodies(s).Length);Assert.AreEqual(80,s.Snapshot().Energy);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="construct-rejected"&&e.Result.StartsWith("OwnerLimit")));}
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=2,Z=0,Width=1,Depth=1}};
            using(var s=Simulation(p)){s.Cast("barrel-chain",2,0);s.Advance(1);Assert.AreEqual(0,Bodies(s).Length);Assert.AreEqual(90,s.Snapshot().Energy);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Result.StartsWith("BlockedPosition")));}
        }
        [Test]
        public void WorldLimitAndLargeDeathWaveStayBoundedAndDoNotReenterTheSameObject()
        {
            var p=Example();Node(p).Parameters["count"]=16;Node(p).Parameters["maxAlive"]=128;Node(p).Parameters["spacing"]=.01f;Cast(p).Parameters["duration"]=.02f;
            using(var s=Simulation(p))
            {
                for(int i=0;i<9;i++){Assert.IsTrue(s.Cast("barrel-chain",0,0).Accepted);s.Advance(1);}Assert.AreEqual(128,Bodies(s).Length);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Result.StartsWith("WorldLimit")));
                s.Cast("detonate-constructs",0,0);s.Advance(1);Assert.AreEqual(0,Bodies(s).Length);Assert.AreEqual(128,s.Snapshot().Statistics.Constructs);
                Assert.AreEqual(100000-128*12,s.Snapshot().Actors.Single(a=>a.Kind=="target").Hp);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Result.StartsWith("DeathWaveLimit")));
            }
        }
        [Test]
        public void DeathWaveBudgetRejectsCallbackCyclesWithoutRecursiveStackGrowth()
        {
            var world=World(out _);
            try
            {
                int died=0,rejected=0;world.Events.Subscribe<EvConstructLifecycle>(e=>{if(e.Kind==ConstructEventKind.Died)died++;if(e.Kind==ConstructEventKind.Rejected)rejected++;});
                var definition=new ConstructDefinition{SpecId=1,Group="cycle",Hp=1,DecayPeriod=0};
                definition.OnDeath=new IEffect[]{new CallbackEffect(()=>{var child=Body(world,null,definition);Damage(world,child,child,1);})};
                var first=Body(world,null,definition);Damage(world,first,first,1);world.FlushCleanup();Assert.AreEqual(256,died);Assert.AreEqual(1,rejected);Assert.AreEqual(0,world.RegistryActive().Count);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void PositionedExplosionCueCarriesTheActualBodyPosition()
        {
            var world=World(out var loaded);
            try
            {
                loaded.Content.Constructs.TryGet(5001,out var definition);var owner=Spawn(world,"arena-player",-6);var body=Body(world,owner,definition,3);SimVec3 point=default;bool seen=false;
                world.Events.Subscribe<EvCue>(e=>{if(e.CueId==4206){seen=e.HasPoint;point=e.Point;}});Damage(world,body,body,9999);Assert.IsTrue(seen);Assert.AreEqual(3,point.X);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void DeathListenersAfterTheConstructStillReceiveTheOriginalEvent()
        {
            var world=World(out _);
            try
            {
                var body=Body(world,null,new ConstructDefinition{SpecId=1,Group="test",Hp=1,DecayPeriod=0});int observed=0;
                world.Events.Subscribe<EvConstructLifecycle>(e=>{});world.Events.Subscribe<EvEntityDead>(e=>{if(e.Id==body.Id)observed++;});
                Damage(world,body,body,1);Assert.AreEqual(1,observed);
            }
            finally{world.Shutdown();}
        }
        [TestCase("hp")] [TestCase("radius")] [TestCase("group")] [TestCase("period")] [TestCase("cap")] [TestCase("view")]
        public void InvalidConstructFieldsRejectThePublication(string field)
        {
            var p=Example();var n=Node(p).Parameters;if(field=="hp")n["hp"]=-1;if(field=="radius")n["radius"]=float.NaN;if(field=="group")n["group"]=" ";
            if(field=="period")n["decayPeriod"]=.001f;if(field=="cap")n["damageCap"]=-1;if(field=="view")n["view"]="missing";
            Assert.Throws<ContentValidationException>(()=>SkillPublicationJson.Create(p,"barrel-chain"));
        }
        [Test]
        public void AHighTeamMaskIsAnExactIntegerAndFiltersTheSelectedTeam()
        {
            foreach(int mask in new[]{1<<30,int.MaxValue})
            {
                var p=Example();p.Skills.Single(s=>s.Id=="barrel-chain").Nodes.Single(n=>n.Kind=="delivery").Parameters["hostileMask"]=mask;
                var content=CombatContentCompiler.Compile(p);var world=World(out _);
                try
                {
                    var owner=Spawn(world,"arena-player",-6);var high=Spawn(world,"arena-target",.5f);high.GetComp<TeamComp>().SetTeam(30);high.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);
                    var other=Spawn(world,"arena-target",1);other.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);content.Constructs.TryGet(Node(p).RuntimeId,out var definition);
                    var body=Body(world,owner,definition);Damage(world,body,body,9999);Assert.AreEqual(992.5f,Hp(high),.001f);Assert.AreEqual(mask==int.MaxValue?992.5f:1000,Hp(other),.001f);
                }
                finally{world.Shutdown();}
            }
        }
        [Test]
        public void EnemyTeamConstructsDoNotReduceTheArenaEnemySpawnBudget()
        {
            using(var s=new ArenaProject(SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"))).Create())
            {
                s.Start();s.World.TryGetActor(s.PlayerId,out var player);Body(s.World,player,new ConstructDefinition{SpecId=1,Group="test",Team=2,DecayPeriod=0},10);
                s.Advance();Assert.AreEqual(10,s.EnemyCount);Assert.AreEqual(10,s.SpawnedEnemies);
            }
        }
        [Test]
        public void NestedDuplicateConstructIdentityReportsTheNodePath()
        {
            var p=Example();var graph=p.Skills.Single(s=>s.Id=="barrel-chain");var copy=new SkillNodeDocument{Id="child",Kind="construct",RuntimeId=Node(p).RuntimeId,Parameters=(Newtonsoft.Json.Linq.JObject)Node(p).Parameters.DeepClone()};
            graph.Nodes=graph.Nodes.Concat(new[]{copy}).ToArray();graph.Edges=graph.Edges.Concat(new[]{new SkillEdgeDocument{Id="child-edge",Source="barrel",SourcePort="death",Target="child",TargetPort="execute",Order=2}}).ToArray();
            var error=Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));StringAssert.Contains("child",error.Path);StringAssert.Contains("runtimeId",error.Path);
        }
    }
}
