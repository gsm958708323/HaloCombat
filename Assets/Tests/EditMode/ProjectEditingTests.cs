using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ProjectEditingTests
    {
        static SkillProjectDocument Example()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SkillEditorDocument Editor(string id)=>new SkillEditorDocument {SelectedSkill=id,Viewports=new[]{new GraphViewportDocument {Kind="skill",Id=id,X=100,Y=-50,Zoom=.7f}}};

        [Test]
        public void EmptyProjectIsPersistableButNeverCompilesAsRuntimeContent()
        {
            var p=Example();p.Skills=Array.Empty<SkillGraphDocument>();p.Buffs=Array.Empty<BuffDocument>();p.SummonBlueprints=Array.Empty<SummonBlueprintDocument>();
            SkillProjectStructure.Validate(SkillProjectJson.Read(SkillProjectJson.Write(p)));
            var error=Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));StringAssert.Contains("$.skills",error.Message);
            Assert.Throws<ContentValidationException>(()=>SkillPublicationJson.Create(p,"missing"));
        }
        [Test]
        public void DisconnectedDraftPreservesNodesAndEdgesWithoutBecomingRunnable()
        {
            var p=Example();p.Skills[0].Edges=Array.Empty<SkillEdgeDocument>();
            SkillProjectStructure.Validate(p);var roundtrip=SkillProjectJson.Read(SkillProjectJson.Write(p));
            Assert.AreEqual(p.Skills[0].Nodes.Length,roundtrip.Skills[0].Nodes.Length);
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(roundtrip));
        }
        [Test]
        public void EditorMetadataRoundtripsAndRejectsUnknownFields()
        {
            var p=Example();p.Editor=Editor(p.Skills[0].Id);var roundtrip=SkillProjectJson.Read(SkillProjectJson.Write(p));SkillProjectStructure.Validate(roundtrip);
            Assert.AreEqual(100,roundtrip.Editor.Viewports[0].X);Assert.AreEqual(.7f,roundtrip.Editor.Viewports[0].Zoom);
            var json=JObject.Parse(SkillProjectJson.Write(p));json["editor"]["unexpected"]=1;Assert.Throws<ContentValidationException>(()=>SkillProjectJson.Read(json.ToString()));
        }
        [TestCase(0f)] [TestCase(5f)] [TestCase(float.NaN)]
        public void InvalidViewportZoomIsRejected(float zoom)
        {
            var p=Example();p.Editor=Editor(p.Skills[0].Id);p.Editor.Viewports[0].Zoom=zoom;
            StringAssert.Contains("viewport",Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p)).Message);
        }
        [Test]
        public void ViewportsRequireUniqueExistingGraphsAndValidSelection()
        {
            var p=Example();p.Editor=Editor(p.Skills[0].Id);p.Editor.Viewports=new[]{p.Editor.Viewports[0],p.Editor.Viewports[0]};
            StringAssert.Contains("Duplicate",Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p)).Message);
            p.Editor=Editor("missing");StringAssert.Contains("selectedSkill",Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p)).Message);
        }
        [Test]
        public void LayoutAndEditorSelectionDoNotChangePublicationRevisionOrMutateSource()
        {
            var p=Example();var before=SkillPublicationJson.Create(p,"basic-shot");p.Editor=Editor("burst-shot");
            foreach(var graph in p.Skills)foreach(var node in graph.Nodes){node.Position.X+=123;node.Position.Y-=45;}
            var savedX=p.Skills[0].Nodes[0].Position.X;var after=SkillPublicationJson.Create(p,"basic-shot");
            Assert.AreEqual(before.Revision,after.Revision);Assert.IsNull(after.Project.Editor);
            Assert.IsTrue(after.Project.Skills.SelectMany(s=>s.Nodes).All(n=>n.Position.X==0&&n.Position.Y==0));
            Assert.AreEqual(savedX,p.Skills[0].Nodes[0].Position.X);Assert.AreNotSame(p.Skills[0].Nodes[0].Parameters,after.Project.Skills[0].Nodes[0].Parameters);
        }
        [Test]
        public void TemplatesKeepAuthoringPositionsWhilePublicationsNormalizeTheirOwnDetachedCopy()
        {
            var p=Example();p.Editor=Editor("basic-shot");var source=p.Skills[0];var template=SkillProjectJson.SelectTemplate(p,"basic-shot");
            CollectionAssert.AreEqual(source.Nodes.Select(n=>n.Position.X),template.Skills[0].Nodes.Select(n=>n.Position.X));
            Assert.Greater(template.Skills[0].Nodes.Select(n=>n.Position.X).Distinct().Count(),1);
            var publication=SkillPublicationJson.Create(template,"basic-shot");Assert.IsTrue(publication.Project.Skills[0].Nodes.All(n=>n.Position.X==0));
            Assert.AreNotSame(source.Nodes[0].Parameters,template.Skills[0].Nodes[0].Parameters);Assert.IsNull(template.Editor);
            CollectionAssert.AreEqual(source.Nodes.Select(n=>n.Position.X),template.Skills[0].Nodes.Select(n=>n.Position.X));
        }
        [Test]
        public void DraftValidationRejectsUnknownKindsDanglingEndpointsAndDuplicateIdentities()
        {
            var p=Example();p.Skills[0].Nodes[0].Kind="unknown";Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));
            p=Example();p.Skills[0].Edges[0].Target="missing";Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));
            p=Example();p.Skills[1].Id=p.Skills[0].Id;Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));
        }
        [Test]
        public void DisabledSharedReferencesAreCheckedSoDeletingAnIdCannotLeaveHiddenDanglingUses()
        {
            var p=Example();p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{new SkillNodeDocument {Id="disabled",Kind="buff",Enabled=false,Parameters=new JObject{{"buff","missing"}}}}).ToArray();
            var error=Assert.Throws<ContentValidationException>(()=>SkillProjectStructure.Validate(p));StringAssert.Contains("disabled",error.Message);StringAssert.Contains("buff",error.Message);
        }
        [Test]
        public void SharedReferencesMayBeUnselectedInDraftButNotPublished()
        {
            var p=Example();var node=p.Skills.First(s=>s.Nodes.Any(n=>n.Kind=="buff")).Nodes.First(n=>n.Kind=="buff");node.Parameters["buff"]="";
            SkillProjectStructure.Validate(p);Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
    }
}
