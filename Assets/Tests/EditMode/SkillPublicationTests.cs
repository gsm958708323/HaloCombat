using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillPublicationTests
    {
        static SkillProjectDocument Example() => SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
        static string Publish(SkillProjectDocument project, string key) => SkillPublicationJson.Write(SkillPublicationJson.Create(project, key));
        static Dictionary<string, string> Slots(SkillProjectDocument project) => new Dictionary<string, string>
        { { "f6", Publish(project, "basic-shot") }, { "f7", Publish(project, "burst-shot") } };

        [Test]
        public void PublicationSelectsANonFirstSkillAndRejectsStaleContent()
        {
            var text = Publish(Example(), "burst-shot");
            Assert.AreEqual("burst-shot", SkillPublicationJson.Read(text).Document.SkillId);
            Assert.AreEqual(1,SkillPublicationJson.Read(text).Document.Project.Skills.Length,"Publication contains the selected dependency closure.");
            var changed = JObject.Parse(text);
            changed["project"]["skills"][0]["nodes"][1]["parameters"]["cost"] = 35;
            var error = Assert.Throws<ContentValidationException>(() => SkillPublicationJson.Read(changed.ToString()));
            Assert.AreEqual("$.revision", error.Path);
        }

        [Test]
        public void BothSlotsShareActorEnergyAndCastingGuards()
        {
            var project = Example(); project.Preview.Energy = 15; project.Preview.Recovery = 0;
            using (var session = new PublishedSkillSession())
            {
                session.Replace(Slots(project));
                Assert.AreEqual("burst-shot", session.Slots["f7"]);
                Assert.IsTrue(session.Simulation.Cast(session.Slots["f6"], 0, 0).Accepted);
                Assert.IsFalse(session.Simulation.Cast(session.Slots["f7"], 0, 0).Accepted);
                session.Simulation.Advance(60);
                Assert.IsFalse(session.Simulation.Cast(session.Slots["f7"], 0, 0).Accepted);
                Assert.AreEqual(5, session.Simulation.Snapshot().Energy);
                Assert.AreEqual(1, session.Simulation.Snapshot().Statistics.Casts);
            }
        }

        [Test]
        public void FailedReloadPreservesLiveObjectsAndSuccessfulReloadDisposesTheOldWorld()
        {
            var project = Example();
            using (var session = new PublishedSkillSession())
            {
                session.Replace(Slots(project));
                var original = session.Simulation; string revision = session.Revision;
                original.Cast("basic-shot", 0, 0); original.Advance(15);
                Assert.IsTrue(original.Snapshot().Actors.Any(actor => actor.Kind == "projectile"));
                var invalid = Slots(project); invalid["f7"] = "{broken json";
                var error = Assert.Throws<ContentValidationException>(() => session.Replace(invalid));
                StringAssert.Contains("f7", error.Path);
                Assert.AreSame(original, session.Simulation); Assert.AreEqual(revision, session.Revision);
                original.Advance(45);
                Assert.AreEqual(80, original.Snapshot().Statistics.Damage);
                project.Skills[0].Nodes[3].Parameters["flat"] = 125;
                session.Replace(Slots(project));
                Assert.AreNotEqual(revision, session.Revision);
                Assert.AreEqual(0, session.Simulation.Snapshot().Step);
                Assert.AreEqual(0, session.Simulation.Snapshot().Statistics.Projectiles);
                Assert.Throws<ObjectDisposedException>(() => original.Advance(1));
            }
        }

        [Test]
        public void MissingSelectionInvalidSceneAndConflictingVersionsAreRejected()
        {
            var project = Example();
            Assert.Throws<ContentValidationException>(() => Publish(project, "missing"));
            project.Preview.Targets[0].Hp = float.NaN;
            var error = Assert.Throws<ContentValidationException>(() => Publish(project, "basic-shot"));
            StringAssert.Contains("preview.targets[0].hp", error.Path);
            project = Example();
            var first = Publish(project, "basic-shot");
            project.Skills[0].Nodes[3].Parameters["flat"] = 100;
            using (var session = new PublishedSkillSession())
                Assert.Throws<ContentValidationException>(() => session.Replace(new Dictionary<string, string>
                { { "f6", first }, { "f7", Publish(project, "basic-shot") } }));
        }
    }
}
