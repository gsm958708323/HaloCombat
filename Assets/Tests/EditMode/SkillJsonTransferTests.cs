using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillJsonTransferTests
    {
        static SkillProjectDocument Example() => Read("Content/SkillLab/project.json");
        static SkillProjectDocument Read(string path) => SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../"+path)));

        [Test]
        public void ProjectFilesKeepDraftsAndEvenIntentionallyOverlappingLayout()
        {
            var source=Example();source.Editor=new SkillEditorDocument{SelectedSkill="basic-shot"};
            source.Skills[0].Edges=Array.Empty<SkillEdgeDocument>();
            foreach(var node in source.Skills[0].Nodes)node.Position=new NodePosition();
            string original=SkillProjectJson.Write(source);var export=SkillJsonTransfer.Export(source,"project");
            var imported=SkillJsonTransfer.Import(export.Json);
            Assert.AreEqual(original,SkillProjectJson.Write(imported.Project));
            Assert.IsFalse(imported.NeedsLayout,"A project file owns its authored positions, including zero.");
            Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Export(source,"runtime"));
            var empty=new SkillProjectDocument{Id="empty"};
            Assert.AreEqual(0,SkillJsonTransfer.Import(SkillJsonTransfer.Export(empty,"project").Json).Project.Skills.Length);
        }

        [Test]
        public void SelectedSkillFileContainsOnlyItsDependencyClosureAndRestoresTheRoot()
        {
            var source=Example();string before=SkillProjectJson.Write(source);
            var export=SkillJsonTransfer.Export(source,"skill","summon-gunners");
            var publication=SkillPublicationJson.Read(export.Json).Document;
            CollectionAssert.AreEquivalent(new[]{"summon-gunners","basic-shot"},publication.Project.Skills.Select(s=>s.Id));
            Assert.AreEqual(1,publication.Project.SummonBlueprints.Length);
            Assert.AreEqual(0,publication.Project.Buffs.Length);
            Assert.AreEqual(export.Revision,publication.Revision);
            var imported=SkillJsonTransfer.Import(export.Json);
            Assert.AreEqual("summon-gunners",imported.Project.Editor.SelectedSkill);
            Assert.IsTrue(imported.NeedsLayout);
            Assert.AreEqual(before,SkillProjectJson.Write(source),"Export must not zero the live editor's graph positions.");
        }

        [Test]
        public void CompleteRuntimeKeepsAllFormalContentAndExcludesOnlyEditorMetadata()
        {
            var source=Read("Assets/StreamingAssets/Combat/project.json");
            source.Editor=new SkillEditorDocument{SelectedSkill=source.Skills.Last().Id};string before=SkillProjectJson.Write(source);
            var export=SkillJsonTransfer.Export(source,"runtime");var raw=JObject.Parse(export.Json);
            Assert.IsNull(raw["editor"]);
            Assert.IsFalse(raw.SelectTokens("$..nodes[*]").Any(n=>n["position"]!=null));
            var expected=JObject.Parse(before);expected.Property("editor").Remove();
            foreach(var node in expected.SelectTokens("$..nodes[*]"))((JObject)node).Property("position")?.Remove();
            Assert.IsTrue(JToken.DeepEquals(expected,raw),"All identities, parameters, order, resources and Arena config must survive.");
            var imported=SkillJsonTransfer.Import(export.Json);
            Assert.IsTrue(imported.NeedsLayout);Assert.AreEqual(source.Skills.Length,export.Skills);
            Assert.AreEqual(source.Runtime.Actors.Length,export.Actors);
            CombatContentCompiler.Compile(imported.Project);
            Assert.AreEqual(before,SkillProjectJson.Write(source));
            source.Skills[0].Nodes[0].Position.X+=100;source.Editor=null;
            Assert.AreEqual(export.Revision,SkillJsonTransfer.Export(source,"runtime").Revision,"Editor-only edits cannot change runtime revision.");
        }

        [TestCase("project")]
        [TestCase("skill")]
        [TestCase("runtime")]
        public void ExportImportPreservesRealExecution(string kind)
        {
            var source=kind=="runtime"?Read("Assets/StreamingAssets/Combat/project.json"):Example();
            string skill=kind=="runtime"?"arena-ammo-shot":"summon-gunners";
            var transferred=SkillJsonTransfer.Import(SkillJsonTransfer.Export(source,kind,skill).Json).Project;
            using(var original=new SkillSimulation(source,CombatContentCompiler.Compile(source)))
            using(var loaded=new SkillSimulation(transferred,CombatContentCompiler.Compile(transferred)))
            {
                Assert.IsTrue(original.Cast(skill,0,0).Accepted);
                Assert.IsTrue(loaded.Cast(skill,0,0).Accepted);
                original.Advance(120);loaded.Advance(120);
                var expected=JObject.FromObject(original.Snapshot());var cooldowns=(JObject)expected["Cooldowns"];
                foreach(var item in cooldowns.Properties().Where(p=>!transferred.Skills.Any(s=>s.Id==p.Name)).ToArray())item.Remove();
                Assert.IsTrue(JToken.DeepEquals(expected,JObject.FromObject(loaded.Snapshot())),
                    "The downloaded format must reproduce actual summons, AI casts, projectiles, damage and provenance.");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("[]")]
        [TestCase("{\"formatVersion\":1,\"formatVersion\":1,\"id\":\"duplicate\"}")]
        [TestCase("{\"formatVersion\":2,\"id\":\"future\"}")]
        [TestCase("{\"formatVersion\":1,\"id\":\"unknown\",\"unsupported\":true}")]
        public void InvalidRawFilesAreRejectedBeforeAnEditorReplacement(string text)
            =>Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Import(text));

        [Test]
        public void ACorruptPublicationCannotFallBackToProjectImport()
        {
            var raw=JObject.Parse(SkillJsonTransfer.Export(Example(),"skill","basic-shot").Json);
            raw["revision"]="tampered";
            Assert.AreEqual("$.revision",Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Import(raw.ToString())).Path);
            raw.Property("revision").Remove();
            Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Import(raw.ToString()));
        }

        [Test]
        public void InvalidRuntimeGraphsAndUnknownExportKindsCannotProduceRuntimeFiles()
        {
            var source=Example();source.Skills[0].Nodes.Single(n=>n.Kind=="projectile").Parameters["speed"]=-1;
            foreach(string kind in new[]{"skill","runtime"})
                Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Export(source,kind,"basic-shot"));
            Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Export(Example(),"unsupported"));
        }

        [Test]
        public void DownloadNameIsASafeLeafWithoutChangingContentIdentity()
        {
            var source=Read("Assets/StreamingAssets/Combat/project.json");
            foreach(string identity in new[]{"../../folder/raw:identity*?","CON","lpt1"})
            foreach(string kind in new[]{"project","skill","runtime"})
            {
                source.Id=identity;
                var export=SkillJsonTransfer.Export(source,kind,source.Skills[0].Id);
                Assert.AreEqual(export.Filename,Path.GetFileName(export.Filename));
                Assert.IsFalse(export.Filename.Any(c=>Path.GetInvalidFileNameChars().Contains(c)));
                Assert.IsFalse(export.Filename.StartsWith("CON.",StringComparison.OrdinalIgnoreCase)||export.Filename.StartsWith("LPT1.",StringComparison.OrdinalIgnoreCase));
                Assert.AreEqual(source.Id,SkillJsonTransfer.Import(export.Json).Project.Id);
            }
        }

        [Test]
        public void APreviewOnlyProjectCannotPretendToBeCompleteGameContent()
        {
            Assert.AreEqual("$.runtime.actors",Assert.Throws<ContentValidationException>(()=>SkillJsonTransfer.Export(Example(),"runtime")).Path);
        }
    }
}
