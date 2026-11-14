using System;
using System.Collections.Generic;
using Combat.Core;

namespace Combat.Config
{
    public sealed class CombatContentSnapshot
    {
        public SkillCatalog Skills { get; } = new SkillCatalog();
        public TimelineLibrary Timelines { get; } = new TimelineLibrary();
        public ProjectileCatalog Projectiles { get; } = new ProjectileCatalog();
        public AoeCatalog Aoes { get; } = new AoeCatalog();
        public SummonCatalog Summons { get; } = new SummonCatalog();
        public SummonBlueprintCatalog Blueprints {get;}=new SummonBlueprintCatalog();
        public ContentDependencies Dependencies {get;}=new ContentDependencies();
        public BuffCatalog Buffs {get;}=new BuffCatalog();
        public CueLibrary Cues { get; } = new CueLibrary();
        public SkillEventCatalog SkillEvents {get;}=new SkillEventCatalog();
        public RuntimeContentSnapshot Runtime {get;internal set;}
        internal HashSet<int> RequiredCues {get;}=new HashSet<int>();
    }

    public static class CombatContentCompiler
    {
        public static CombatContentSnapshot Compile(SkillProjectDocument project)
        {
            SkillProjectStructure.ValidateRuntimeStructure(project);
            if (project == null || project.FormatVersion != 1) Fail("$.formatVersion", "Only version 1 is supported.");
            if (string.IsNullOrWhiteSpace(project.Id)) Fail("$.id", "Project id is required.");
            SkillSceneValidation.Validate(project.Preview);
            if (project.Skills == null || project.Skills.Length == 0 || project.Skills.Length > 512) Fail("$.skills", "Expected 1 to 512 skills.");
            var content = new CombatContentSnapshot();
            var keys = new HashSet<string>(); var numbers = new HashSet<int>();
            var skillIds=new Dictionary<string,SkillNodeId>();
            var timelineIds=new HashSet<int>();
            for (int i = 0; i < project.Skills.Length; i++)
            {
                var skill = project.Skills[i]; var path = "$.skills[" + i + "]";
                if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !keys.Add(skill.Id)) Fail(path + ".id", "Missing or duplicate skill id.");
                if (skill.RuntimeId <= 0 || !numbers.Add(skill.RuntimeId)) Fail(path + ".runtimeId", "Expected unique positive id.");
                if(!timelineIds.Add(skill.Timeline?.RuntimeId??skill.RuntimeId))Fail(path+".timeline.runtimeId","Duplicate timeline identity.");
                skillIds.Add(skill.Id,new SkillNodeId(skill.RuntimeId));content.Dependencies.Register(ContentDependencies.Skill(skill.Id));
            }
            SummonBlueprintCompiler.Compile(project.SummonBlueprints,skillIds,content.Blueprints);
            BuffCompiler.Register(project.Buffs,content);
            var projectileIds = new HashSet<int>(); var aoeIds = new HashSet<int>();var summonIds=new HashSet<int>();
            for(int i=0;i<project.Skills.Length;i++)
            {
                var skill=project.Skills[i];string path="$.skills["+i+"]";
                var graph = new EffectGraphIndex(ContentDependencies.Skill(skill.Id),skill.Nodes,skill.Edges,path);
                var target = graph.Root("target");
                var t = new NodeParameters(target, graph.NodePath(target), "mode", "range");
                var mode = t.Choice("mode", "point", "point", "entity", "nearest", "random", "direction", "self","facing");
                var range = t.Number("range", 20, 0.01f, 1000);
                var cast = graph.Root("cast");
                var outputs = graph.Outputs(target, "target", "target");
                if (outputs.Count != 1 || outputs[0] != cast) Fail(path, "Target must connect to the cast target port.");
                var compiler = new SkillEffectCompiler(graph, content, projectileIds, aoeIds,summonIds);
                var effects = compiler.Outputs(cast, "cast", true);
                var authored=skill.Timeline==null?null:AuthoredTimelineCompiler.Compile(skill.Timeline,graph,compiler);
                if (authored==null&&effects.Length + compiler.Clips.Count + compiler.Followups.Count == 0) Fail(graph.NodePath(cast), "Cast needs an enabled delivery/effect.");
                var definition = SkillCastCompiler.Compile(skill, cast, graph.NodePath(cast), effects, content.Timelines,compiler.Clips.ToArray(),compiler.Followups.ToArray(),authored,skillIds,content.Dependencies);
                definition.AimRange=compiler.AimRange;
                definition.TargetMode = (SkillTargetMode)Array.IndexOf(new[] { "point", "entity", "nearest", "random", "direction", "self","facing" }, mode);
                definition.TargetRange = range;
                content.Skills.Register(definition);
                var events=graph.OptionalRoot("skill-events");
                if(events!=null)
                {
                    new NodeParameters(events,graph.NodePath(events));
                    var handlers=new SkillEventDefinition();
                    string[] ports={"hit","critical","kill","completed","wall","range","projectile-end"};
                    for(int port=0;port<ports.Length;port++)handlers.Set((SkillEventKind)port,compiler.Outputs(events,ports[port]));
                    content.SkillEvents.Register(definition.Id,handlers);
                }
                graph.CheckComplete();
            }
            for(int i=0;i<project.Buffs.Length;i++)BuffCompiler.Link(project.Buffs[i],"$.buffs["+i+"]",content,projectileIds,aoeIds,summonIds);
            content.Dependencies.Validate();content.Runtime=RuntimeContentCompiler.Compile(project,content);return content;
        }

        static void Fail(string path, string message) => throw new ContentValidationException(path, message);
    }
}
