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
    public sealed class RuntimeContentTests
    {
        static string Path=>System.IO.Path.Combine(Application.streamingAssetsPath,"Combat/project.json");
        static SkillProjectDocument Project()=>SkillProjectJson.Read(File.ReadAllText(Path));
        static CombatWorld World(out RuntimeProjectLoader content){content=new RuntimeProjectLoader(Path);return content.CreateWorld(new FixedRandom(.5f));}
        static Actor Spawn(CombatWorld world,string id,float x){world.TryGetActor(world.SpawnActor(new ActorSpawnSpec(id)),out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,0);return actor;}
        static void Tick(CombatWorld world,int count){for(int i=0;i<count;i++)world.Tick(.02f);}

        [Test]
        public void ActualMigratedContentMatchesTheLegacyAssetSnapshot()
        {
            var content=new RuntimeProjectLoader(Path);
            var baseline=JToken.Parse(File.ReadAllText(System.IO.Path.Combine(Application.dataPath,"Tests/Fixtures/legacy-combat-content.json")));
            var actual=JToken.Parse(RuntimeMigrationSignature.Capture(content.Content.Runtime.Data).ToString());
            Assert.IsTrue(JToken.DeepEquals(baseline,actual),"Migrated numeric values and effect order differ from actual original asset data.");
        }
        [Test]
        public void JsonRoundtripRetainsFourClipKindsAndStableTimelineIdentities()
        {
            var p=Project();var clone=SkillProjectJson.Read(SkillProjectJson.Write(p));var content=CombatContentCompiler.Compile(clone);
            Assert.AreEqual(12,content.Skills.All.Count());Assert.AreEqual(6,content.Skills.All.Count(s=>s.Id.Value<2000));Assert.IsTrue(content.Timelines.TryGet(TimelineId.TL_G1,out var g1));
            Assert.AreEqual(.55f,g1.Duration);Assert.AreEqual(3,g1.Clips.Length);Assert.AreEqual(.18f,g1.Payloads[0].Time);
            Assert.IsTrue(content.Timelines.TryGet(TimelineId.TL_Dodge,out var dodge));Assert.IsTrue(dodge.Clips.Any(c=>c.Kind==ClipKind.IFrame));
        }
        [Test]
        public void EveryRuntimeActorUsesExplicitBlueprintDataAndUnknownIdsFail()
        {
            var world=World(out var loaded);
            foreach(var definition in loaded.Content.Runtime.Actors.Values)
            {var actor=Spawn(world,definition.Id,0);Assert.AreEqual(definition.Hp,actor.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.AreEqual(definition.Team,actor.GetComp<TeamComp>().TeamId);}
            Assert.Throws<InvalidOperationException>(()=>world.SpawnActor(new ActorSpawnSpec("missing")));world.Shutdown();
        }
        [Test]
        public void RuntimeComboUsesTheGuardAndFailedInputRemainsBuffered()
        {
            var world=World(out _);var player=Spawn(world,"fighter",0);var buffer=player.GetComp<InputBufferComp>();
            buffer.Push(InputToken.Attack);Tick(world,1);Assert.AreEqual(SkillNodeId.G1,player.GetComp<SkillDirectorComp>().CurrentSkill);
            buffer.Push(InputToken.Attack);Tick(world,1);Assert.IsTrue(buffer.TryPeek(out _));
            Tick(world,7);Assert.AreEqual(SkillNodeId.G2,player.GetComp<SkillDirectorComp>().CurrentSkill);Assert.IsFalse(buffer.TryPeek(out _));world.Shutdown();
        }
        [Test]
        public void DodgeConsumesOnlyOnSuccessfulCastAndRunsItsInvulnerabilityClip()
        {
            var world=World(out _);var player=Spawn(world,"fighter",0);player.GetComp<InputBufferComp>().Push(Season2Tokens.Dodge);
            Tick(world,4);Assert.AreEqual(SkillNodeId.Dodge,player.GetComp<SkillDirectorComp>().CurrentSkill);Assert.IsTrue(player.GetComp<TagComp>().Has(CommonTags.Invincible));
            player.GetComp<SkillDirectorComp>().Stop(DirectorStopReason.Manual);Assert.IsFalse(player.GetComp<TagComp>().Has(CommonTags.Invincible));world.Shutdown();
        }
        [Test]
        public void ExplicitTimelineHitAndProjectileUseTheActualEffectPipeline()
        {
            var world=World(out _);var player=Spawn(world,"fighter",0);var target=Spawn(world,"stake",.55f);float before=target.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            int damages=0,spawns=0;world.Events.Subscribe<EvDamage>(e=>damages++);world.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId=="projectile")spawns++;});
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);Tick(world,40);
            Assert.Greater(damages,0);Assert.Greater(spawns,0);Assert.Less(target.GetComp<AttributeSet>().GetBase(AttrId.Hp),before);world.Shutdown();
        }
        [Test]
        public void ExplicitTimelineEffectListsPreserveRepeatedExecutionItems()
        {
            var p=Project();p.Skills[0].Timeline.Payloads[0].Effects=new[]{"slash-cue","slash-cue"};var c=CombatContentCompiler.Compile(p);
            Assert.IsTrue(c.Timelines.TryGet(TimelineId.TL_G1,out var timeline));Assert.AreEqual(2,timeline.Payloads[0].Effects.Length);
        }
        [TestCase("kind")] [TestCase("end")] [TestCase("effects")] [TestCase("conflict")]
        public void BadTimelineCannotCompile(string field)
        {
            var p=Project();if(field=="kind")p.Skills[0].Timeline.Clips[0].Kind="unknown";
            if(field=="end")p.Skills[0].Timeline.Clips[0].End=10;
            if(field=="effects")p.Skills[0].Timeline.Payloads[0].Effects=new[]{"missing"};
            if(field=="conflict")p.Skills[1].Timeline.RuntimeId=p.Skills[0].Timeline.RuntimeId;
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void MissingRuntimeFileAndInvalidReferencesNeverUseCodeFallback()
        {
            Assert.Throws<FileNotFoundException>(()=>new RuntimeProjectLoader(Path+".missing"));
            var p=Project();p.Runtime.Combos[0].ToSkill="missing";StringAssert.Contains("toSkill",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [Test]
        public void SharedSummonBlueprintIsTheSingleSourceOfStatsAndAttackSkill()
        {
            var p=Project();p.SummonBlueprints[0].Attack=77;var c=CombatContentCompiler.Compile(p);Assert.AreEqual(77,c.Runtime.Actors["summon"].Attack);
            Assert.AreEqual(SkillNodeId.G1,c.Runtime.Actors["summon"].Skill);
        }
        [Test]
        public void SeparateCompilationsDoNotShareActorsOrBuffEffectObjects()
        {
            var first=new RuntimeProjectLoader(Path);var second=new RuntimeProjectLoader(Path);
            Assert.AreNotSame(first.Content.Runtime.Actors["melee_ai"].Tree,second.Content.Runtime.Actors["melee_ai"].Tree);
            first.Content.Buffs.TryGet("burn",out var a);second.Content.Buffs.TryGet("burn",out var b);Assert.AreNotSame(a.OnPeriod[0],b.OnPeriod[0]);
        }
        [Test]
        public void MissingCueMappingIsRejectedBeforeTheWorldCanRun()
        {
            var p=Project();p.Runtime.Cues=p.Runtime.Cues.Where(c=>c.Id!=101).ToArray();
            StringAssert.Contains("runtime.cues[101]",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [Test]
        public void SlotPublicationPreservesRuntimeMotorAndCuesAndRejectsConflictingVersions()
        {
            var p=Project();p.Runtime.Motor.Gravity=-30;
            var slots=new System.Collections.Generic.Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"g1"))},{"f7",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"g2"))}};
            using(var session=new PublishedSkillSession())
            {
                session.Replace(slots);var simulation=typeof(SkillSimulation).GetField("_world",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(session.Simulation) as CombatWorld;
                Assert.AreEqual(-30,simulation.Motor.Gravity);Assert.IsTrue(simulation.Cues.TryGet(101,out var cue));Assert.AreEqual(.4f,cue.LifeTime);
                p.Runtime.Motor.Gravity=-40;slots["f7"]=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"g2"));
                Assert.Throws<ContentValidationException>(()=>session.Replace(slots));Assert.AreEqual(-30,simulation.Motor.Gravity);
            }
        }
    }
}
