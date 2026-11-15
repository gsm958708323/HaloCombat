using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class BodyResourceTests
    {
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        [Test]
        public void OriginalBodiesResolveExplicitResourceKeysAndKeepOriginalSpinAndCues()
        {
            var p=Formal();var c=CombatContentCompiler.Compile(p);Assert.AreEqual(13,c.Runtime.Views.Count);Assert.AreEqual(11,c.Runtime.Views.Values.Count(v=>v.Kind=="body"));
            Assert.IsTrue(c.Projectiles.TryGet(4001,out var shot));Assert.AreEqual("buff_projectile_normal_view",shot.View);Assert.IsTrue(c.Projectiles.TryGet(4003,out var returning));Assert.AreEqual("buff_projectile_boomerang_view",returning.View);
            Assert.AreEqual(720,c.Runtime.Views[returning.View].SpinSpeed);Assert.IsTrue(c.Aoes.TryGet(4105,out var area));Assert.AreEqual("buff_aoe_stayingbomb_view",area.View);
            foreach(int id in Enumerable.Range(4201,12)){Assert.IsTrue(c.Runtime.Data.Cues.TryGet(id,out var cue));Assert.AreEqual("prefab",cue.PresentationKind);Assert.AreEqual(.8f,cue.LifeTime);Assert.IsTrue(string.IsNullOrEmpty(cue.SfxKey),"No original AudioClip exists in the source commit.");}
        }
        [TestCase("missing")] [TestCase("kind")] [TestCase("state")] [TestCase("spin")] [TestCase("runtime")] [TestCase("anchor")]
        public void ResourceReferencesAndAnchorMetadataAreValidatedBeforeRuntimeConstruction(string fault)
        {
            var p=Formal();var view=p.Runtime.Views.Single(v=>v.Id=="buff_projectile_normal_view");
            if(fault=="missing")p.Runtime.Views=p.Runtime.Views.Where(v=>v.Id!=view.Id).ToArray();
            if(fault=="kind")view.Kind="unknown";
            if(fault=="state")view.States["Stand"]="Idle";
            if(fault=="spin")view.SpinSpeed=float.NaN;
            if(fault=="runtime")p.Runtime=null;
            if(fault=="anchor")p.Runtime.Views[0].Anchors=new[]{new RuntimeAnchorDocument{Key="Body",Path="../body"}};
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void CompiledAnchorDefinitionsAreIndependentAndLoopEventsUseTheActualVictim()
        {
            var p=Formal();var content=CombatContentCompiler.Compile(p);var view=p.Runtime.Views[0];string path=view.Anchors[0].Path;view.Anchors[0].Path="changed";Assert.AreEqual(path,content.Runtime.Views[view.Id].Anchors[0].Path);
            var world=new CombatWorld(content.Runtime.CreateFactory(content.Skills));try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var source);world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-target")),out var target);EvCue last=default;world.Events.Subscribe<EvCue>(e=>last=e);
                var origin=new SkillOrigin(new SkillNodeId(2001),5,caster:source.Id);
                world.Deliver(new IEffect[]{new CuePlaybackEffect(4204,"hit",EffectCenter.Target,CuePlayback.Once,null,false,"Body")},source,target,0,origin:origin);
                Assert.AreEqual(target.Id,last.Source);Assert.AreEqual("Body",last.Anchor);Assert.AreEqual(5,last.Origin.CastId);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void PublicationsExposeEachLoadedSlotVersionAndRejectResourcesBeforeReplacingAWorld()
        {
            var p=Formal();var pub=SkillPublicationJson.Create(p,"arena-ammo-shot");var json=SkillPublicationJson.Write(pub);
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",json}});Assert.AreEqual(pub.Revision,session.SlotRevisions["f6"]);var old=session.Simulation;
                session.ValidateViews=_=>throw new ContentValidationException("resource","Invalid Unity resource");
                Assert.Throws<ContentValidationException>(()=>session.Replace(new Dictionary<string,string>{{"f6",json}}));Assert.AreSame(old,session.Simulation);Assert.AreEqual(pub.Revision,session.SlotRevisions["f6"]);
            }
        }
        [Test]
        public void RevisionChecksThePublishedPayloadBeforeCrossRuntimeFloatRounding()
        {
            var p=Formal();var pub=SkillPublicationJson.Create(p,"arena-ammo-shot");var payload=JObject.Parse(SkillPublicationJson.Write(pub));
            payload["project"]["preview"]["targets"][0]["z"]=2.9133646d;
            string value=(string)payload["skillId"]+"\n"+payload["project"].ToString(Formatting.Indented);
            using(var sha=System.Security.Cryptography.SHA256.Create())payload["revision"]=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
            Assert.DoesNotThrow(()=>SkillPublicationJson.Read(payload.ToString(Formatting.None)));
            payload["project"]["preview"]["targets"][0]["z"]=2.9d;Assert.Throws<ContentValidationException>(()=>SkillPublicationJson.Read(payload.ToString()));
        }
    }
}
