using System;
using Newtonsoft.Json;

namespace Combat.Config
{
    public sealed class SkillPublicationDocument
    {
        [JsonProperty(Required = Required.Always)] public int FormatVersion { get; set; } = 1;
        [JsonProperty(Required = Required.Always)] public string Revision { get; set; }
        [JsonProperty(Required = Required.Always)] public string SkillId { get; set; }
        [JsonProperty(Required = Required.Always)] public SkillProjectDocument Project { get; set; }
    }

    public sealed class CompiledSkillPublication
    {
        public SkillPublicationDocument Document { get; }
        public CombatContentSnapshot Content { get; }
        internal CompiledSkillPublication(SkillPublicationDocument document, CombatContentSnapshot content)
        { Document = document; Content = content; }
    }

    public static class SkillPublicationJson
    {
        public static SkillPublicationDocument Create(SkillProjectDocument project, string skillId)
        {
            var content = CombatContentCompiler.Compile(project);
            CheckSkill(content, skillId);
            var selected=SkillContentClosure.Select(project,content,skillId);
            foreach(var graph in selected.Skills)foreach(var node in graph.Nodes)node.Position=new NodePosition();
            foreach(var graph in selected.Buffs)foreach(var node in graph.Nodes)node.Position=new NodePosition();
            foreach(var graph in selected.Timelines)foreach(var node in graph.Nodes)node.Position=new NodePosition();
            foreach(var graph in selected.Projectiles)foreach(var node in graph.Nodes)node.Position=new NodePosition();
            return new SkillPublicationDocument { Project = selected, SkillId = skillId, Revision = Revision(selected, skillId) };
        }

        public static string Write(SkillPublicationDocument publication) => SkillJsonCodec.Write(publication);

        public static CompiledSkillPublication Read(string text)
        {
            var document = SkillJsonCodec.Read<SkillPublicationDocument>(text);
            if (document.FormatVersion != 1) throw new ContentValidationException("$.formatVersion", "Only publication version 1 is supported.");
            var content = CombatContentCompiler.Compile(document.Project);
            CheckSkill(content, document.SkillId);
            if (!string.Equals(document.Revision, Revision(document.Project, document.SkillId), StringComparison.Ordinal))
                throw new ContentValidationException("$.revision", "Published content has changed. Publish the edited project again.");
            return new CompiledSkillPublication(document, content);
        }

        static void CheckSkill(CombatContentSnapshot content, string key)
        {
            foreach (var skill in content.Skills.All) if (skill.Key == key) return;
            throw new ContentValidationException("$.skillId", "Published skill is missing from the project: " + key);
        }

        static string Revision(SkillProjectDocument project, string skillId)
            => SkillJsonCodec.Fingerprint(skillId + "\n" + SkillProjectJson.Write(project));
    }
}
