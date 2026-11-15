using System;
using System.IO;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ActorViewContentTests
    {
        static SkillProjectDocument Project()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        [Test]
        public void FormalActorViewsCompileToIndependentReadOnlyBindingsAndKeepTheAuthoredAnimation()
        {
            var project=Project();var content=CombatContentCompiler.Compile(project);var view=content.Runtime.Views["arena-player-view"];
            Assert.AreEqual("arena-player-view",content.Runtime.Actors["arena-player"].View);Assert.AreEqual("Combat/Actors/FemaleGunner",view.Resource);
            project.Runtime.Views[0].States["Fire"]="changed";project.Runtime.Views[0].Scale=2;
            Assert.AreEqual("Base Layer.Fire",view.States["Fire"]);Assert.AreEqual(1,view.Scale);
            Assert.IsTrue(content.Timelines.TryGet(new Combat.Core.TimelineId(3009),out var reload));Assert.AreEqual("Reload",reload.Animation);
        }
        [TestCase("../outside")] [TestCase("/absolute")] [TestCase("C:/outside")] [TestCase("Combat/Actor.prefab")] [TestCase("Combat\\Actor")] [TestCase("Combat//Actor")]
        public void UnsafeResourceKeysAreRejectedBeforeUnity(string key)
        {
            var p=Project();p.Runtime.Views[0].Resource=key;
            StringAssert.Contains(".resource",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [TestCase("reserved")] [TestCase("duplicate")] [TestCase("body")] [TestCase("action")] [TestCase("direction")] [TestCase("scale")] [TestCase("actor")] [TestCase("animation")]
        public void InvalidViewContractsFailAtTheContentBoundary(string fault)
        {
            var p=Project();var v=p.Runtime.Views[0];
            if(fault=="reserved")v.Id="fighter";
            if(fault=="duplicate")p.Runtime.Views[1].Id=v.Id;
            if(fault=="body")v.States.Remove("Dead");
            if(fault=="action")v.States.Remove("Fire");
            if(fault=="direction")v.States.Remove("RollLeft");
            if(fault=="scale")v.Scale=float.NaN;
            if(fault=="actor")p.Runtime.Actors[0].View="missing";
            if(fault=="animation")p.Skills[0].Timeline.Animation=" ";
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void PublicationRetainsViewCatalogAndActionSemanticAndConflictsKeepTheOldSession()
        {
            var p=Project();var first=SkillPublicationJson.Create(p,"arena-ammo-shot");var text=SkillPublicationJson.Write(first);
            var read=SkillPublicationJson.Read(text);Assert.AreEqual(p.Runtime.Views.Length,read.Content.Runtime.Views.Count);
            Assert.AreEqual("Fire",read.Document.Project.Skills.Single(s=>s.Id=="arena-ammo-shot").Timeline.Animation);
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new System.Collections.Generic.Dictionary<string,string>{{"f6",text}});var previous=session.Simulation;
                p.Runtime.Views[0].YawOffset=30;
                var other=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-reload-ammo"));
                Assert.Throws<ContentValidationException>(()=>session.Replace(new System.Collections.Generic.Dictionary<string,string>{{"f6",text},{"f7",other}}));
                Assert.AreSame(previous,session.Simulation);
            }
            var old=Project();Assert.Throws<ContentValidationException>(()=>new ArenaProject(old,new System.Collections.Generic.Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-ammo-shot"))}}));
        }
        [Test]
        public void OptionalViewAndAnimationFieldsDoNotChangeUnanimatedDocuments()
        {
            var project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            string json=SkillProjectJson.Write(project);StringAssert.DoesNotContain("\"views\"",json);StringAssert.DoesNotContain("\"animation\"",json);
            Assert.NotNull(CombatContentCompiler.Compile(project));
        }
    }
}
