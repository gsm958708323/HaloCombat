using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class CrossWorkspaceCopy
{
    public static SkillJsonImport Prepare(SkillProjectDiskSnapshot source, string skillId)
    {
        var project = source.Project;
        if (string.IsNullOrWhiteSpace(skillId) || !project.Skills.Any(skill => skill.Id == skillId))
            throw new ContentValidationException("$.skillId", "Unknown selected skill: " + skillId);

        var compiled = CompileClosure(project, skillId);
        var selected = SkillContentClosure.Select(project, compiled, skillId);
        selected.Editor = new SkillEditorDocument { SelectedSkill = skillId };
        if (selected.Runtime != null)
        {
            selected.Runtime.Cues = selected.Runtime.Cues?.Where(cue => cue != null && compiled.RequiredCues.Contains(cue.Id)).ToArray();
            selected.Runtime.Views = selected.Runtime.Views?.Where(view => view != null && compiled.RequiredBodyViews.Contains(view.Id)).ToArray();
        }
        RuntimeContentCompiler.Compile(selected, compiled);

        var detached = new List<(SkillNodeDocument Node, SkillJsonDetachedReference Reference)>();
        void Detach(SkillNodeDocument node, string field, string path, string id)
            => detached.Add((node, new SkillJsonDetachedReference(path, node.Id, field, id)));
        var selectedSkills = selected.Skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        for (int i = 0; i < selected.Skills.Length; i++)
        {
            foreach (var node in selected.Skills[i].Nodes.Where(node => node.Kind == "cast" && !node.Enabled))
            {
                foreach (string field in CastReferences)
                {
                    if (node.Parameters[field]?.Type != JTokenType.String) continue;
                    string id = (string)node.Parameters[field];
                    if (id.Length == 0 || selectedSkills.Contains(id)) continue;
                    Detach(node, field, $"$.skills[{i}].nodes[{node.Id}].parameters.{field}", id);
                    node.Parameters.Remove(field);
                }
            }
        }
        SkillProjectStructure.ValidatePublicationImport(selected, Detach);
        foreach (var item in detached) item.Node.Parameters.Remove(item.Reference.Field);
        var document = JObject.Parse(SkillProjectJson.Write(selected));
        return new SkillJsonImport
        {
            Project = selected,
            NeedsLayout = SkillProjectDiskLayout.Nodes(document).Any(node => source.MissingPositions.Contains(node.Key)),
            DetachedReferences = detached.Select(item => item.Reference).ToArray()
        };
    }

    static readonly string[] CastReferences = ["fallbackSkill", "recastSkill"];

    static CombatContentSnapshot CompileClosure(SkillProjectDocument source, string root)
    {
        // This projection starts at the requested skill. The real compiler requests only
        // dependencies it reaches, so dormant references cannot pull in unrelated drafts.
        var skills = new HashSet<string>(StringComparer.Ordinal);
        var timelines = new HashSet<string>(StringComparer.Ordinal);
        var buffs = new HashSet<string>(StringComparer.Ordinal);
        var projectiles = new HashSet<string>(StringComparer.Ordinal);
        var areas = new HashSet<string>(StringComparer.Ordinal);
        var blueprints = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        void AddSkill(string id) { if (!string.IsNullOrWhiteSpace(id) && skills.Add(id)) pending.Enqueue(id); }
        AddSkill(root);
        while (true)
        {
            while (pending.TryDequeue(out string id))
            {
                foreach (var skill in source.Skills.Where(skill => skill.Id == id))
                {
                    if (skill.TimelineRef != null) timelines.Add(skill.TimelineRef);
                    foreach (var node in skill.Nodes.Where(node => node.Kind == "cast" && node.Enabled))
                        foreach (string field in CastReferences)
                            if (node.Parameters[field]?.Type == JTokenType.String) AddSkill((string)node.Parameters[field]);
                }
            }
            var projection = new SkillProjectDocument
            {
                FormatVersion=source.FormatVersion, Id=source.Id, Name=source.Name, Preview=source.Preview,
                Skills=source.Skills.Where(skill => skills.Contains(skill.Id)).ToArray(),
                Timelines=source.Timelines.Where(timeline => timelines.Contains(timeline.Id)).ToArray(),
                Buffs=source.Buffs.Where(buff => buffs.Contains(buff.Id)).ToArray(),
                Projectiles=source.Projectiles.Where(projectile => projectiles.Contains(projectile.Id)).ToArray(),
                Areas=source.Areas.Where(area => areas.Contains(area.Id)).ToArray(),
                SummonBlueprints=source.SummonBlueprints.Where(blueprint => blueprints.Contains(blueprint.Id)).ToArray()
            };
            projection = SkillProjectJson.Read(SkillProjectJson.Write(projection));
            foreach (var node in projection.Skills.SelectMany(skill => skill.Nodes).Where(node => node.Kind == "cast" && !node.Enabled))
                foreach (string field in CastReferences) node.Parameters.Remove(field);
            try
            {
                return CombatContentCompiler.CompileGraphs(projection, (kind, id) =>
                {
                    var added = kind switch
                    {
                        "buffs" => source.Buffs.Any(buff => buff.Id == id) && buffs.Add(id),
                        "projectiles" => source.Projectiles.Any(projectile => projectile.Id == id) && projectiles.Add(id),
                        "areas" => source.Areas.Any(area => area.Id == id) && areas.Add(id),
                        "summonBlueprints" => source.SummonBlueprints.Any(blueprint => blueprint.Id == id) && blueprints.Add(id),
                        _ => false
                    };
                    // When no definition exists, leave the original field error to the compiler.
                    if (added)
                    {
                        if (kind == "summonBlueprints")
                            foreach (var blueprint in source.SummonBlueprints.Where(blueprint => blueprint.Id == id)) AddSkill(blueprint.SkillId);
                        throw new ExpandClosure();
                    }
                });
            }
            catch (ExpandClosure) { }
        }
    }

    sealed class ExpandClosure : Exception { }
}
