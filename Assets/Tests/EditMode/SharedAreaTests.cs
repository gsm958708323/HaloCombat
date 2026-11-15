using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SharedAreaTests
    {
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static SkillNodeDocument Root(SharedAreaDocument d)=>d.Nodes.Single(n=>n.Id==d.Root);
        [Test]
        public void BombPublicationContainsOneCopyOfEachAreaAndItsActualDelayedDependencies()
        {
            var p=Formal();var pub=SkillPublicationJson.Create(p,"arena-bomb-shot");var loaded=SkillPublicationJson.Read(SkillPublicationJson.Write(pub));
            Assert.AreEqual(2007,pub.Project.Skills.Single().RuntimeId);Assert.AreEqual(4005,RootProjectile(pub.Project).RuntimeId);
            CollectionAssert.AreEquivalent(new[]{4104,4105,4106},pub.Project.Areas.Select(a=>Root(a).RuntimeId));Assert.AreEqual(5103,pub.Project.Buffs.Single().RuntimeId);
            Assert.IsTrue(loaded.Content.Aoes.TryGet(4104,out var explosion));Assert.AreEqual(.02f,explosion.Duration);Assert.AreEqual(1.5f,explosion.Radius);
            Assert.IsTrue(loaded.Content.Aoes.TryGet(4105,out var delayed));Assert.AreEqual(3,delayed.Duration);Assert.AreEqual(2,delayed.OnExpire.Length);
            Assert.IsTrue(pub.Project.Areas.SelectMany(a=>a.Nodes).All(n=>n.Position.X==0&&n.Position.Y==0));Assert.Greater(p.Areas[0].Nodes.Max(n=>n.Position.X),0);
        }
        static SkillNodeDocument RootProjectile(SkillProjectDocument p){var d=p.Projectiles.Single();return d.Nodes.Single(n=>n.Id==d.Root);}
        [TestCase("root")] [TestCase("kind")] [TestCase("disabled")] [TestCase("persistent")] [TestCase("center")] [TestCase("offset")] [TestCase("missing")] [TestCase("duplicate")] [TestCase("number")]
        public void SharedAreaBoundaryRejectsInvalidDefinitionsAndReferences(string fault)
        {
            var p=Formal();var area=p.Areas[0];var root=Root(area);
            if(fault=="root")area.Root="missing";
            if(fault=="kind")root.Kind="damage";
            if(fault=="disabled")root.Enabled=false;
            if(fault=="persistent")root.Parameters["persistent"]=false;
            if(fault=="center")root.Parameters["center"]="source";
            if(fault=="offset")root.Parameters["forwardOffset"]=1;
            if(fault=="missing")p.Projectiles.Single(a=>a.Id=="arena-bomb-projectile").Nodes.Single(n=>n.Id=="explosion").Parameters["area"]="missing";
            if(fault=="duplicate")p.Areas=p.Areas.Concat(new[]{area}).ToArray();
            if(fault=="number")Root(p.Areas[1]).RuntimeId=root.RuntimeId;
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void AreasResolveForwardReferencesAndRejectCrossTypeCycles()
        {
            var p=Formal();p.Areas=p.Areas.Reverse().ToArray();Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(p));
            var fire=p.Areas.Single(a=>a.Id=="arena-bomb-fire");fire.Nodes.Single(n=>n.Id=="burn").Kind="area-ref";fire.Nodes.Single(n=>n.Id=="burn").Parameters=JObject.FromObject(new{area="arena-delayed-bomb"});
            StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [Test]
        public void AreaLayoutsKeepVersionsStableAndConflictingSlotsKeepTheCurrentSession()
        {
            var p=Formal();var first=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-bomb-shot"));string revision=SkillPublicationJson.Create(p,"arena-bomb-shot").Revision;
            Root(p.Areas[0]).Position.X=333;Assert.AreEqual(revision,SkillPublicationJson.Create(p,"arena-bomb-shot").Revision);
            using(var s=new PublishedSkillSession())
            {
                s.Replace(new Dictionary<string,string>{{"f6",first},{"f7",first}});var old=s.Simulation;
                Root(p.Areas[0]).Parameters["radius"]=2;var other=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-bomb-shot"));
                StringAssert.Contains("area versions",Assert.Throws<ContentValidationException>(()=>s.Replace(new Dictionary<string,string>{{"f6",first},{"f7",other}})).Message);Assert.AreSame(old,s.Simulation);
                Assert.Throws<ContentValidationException>(()=>new ArenaProject(Formal(),new Dictionary<string,string>{{"f6",first},{"f7",other}}));
            }
        }
        [TestCase("missing-key")] [TestCase("point")] [TestCase("one-shot-key")]
        public void KeyedCueRejectsMissingOwnershipAndAmbiguousPlayback(string fault)
        {
            var p=Formal();var cue=p.Skills.Single(s=>s.Id=="arena-roll").Nodes.Single(n=>n.Id=="roll-start");
            if(fault=="missing-key")cue.Parameters.Remove("instanceKey");
            if(fault=="point")cue.Parameters["center"]="point";
            if(fault=="one-shot-key")cue.Parameters["playback"]="once";
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void ExistingPublicationsWithoutAreasStillValidate()
        {Assert.DoesNotThrow(()=>SkillPublicationJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/f6.json"))));}
    }
}
