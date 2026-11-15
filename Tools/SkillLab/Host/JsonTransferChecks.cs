using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class JsonTransferChecks
{
    public static void Run(string root)
    {
        var source=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));
        var before=SkillProjectJson.Write(source);
        foreach(string kind in new[]{"project","skill","runtime"})
        {
            var document=kind=="runtime"?SkillProjectFiles.Read(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json")):source;
            string skill=kind=="runtime"?"arena-ammo-shot":"summon-gunners";
            using var expected=new SkillSimulation(document,CombatContentCompiler.Compile(document));
            expected.Cast(skill,0,0);expected.Advance(120);var snapshot=JObject.FromObject(expected.Snapshot());
            var file=SkillJsonTransfer.Export(document,kind,skill);
            var imported=SkillJsonTransfer.Import(file.Json);
            using var loaded=new SkillSimulation(imported.Project,CombatContentCompiler.Compile(imported.Project));
            loaded.Cast(skill,0,0);loaded.Advance(120);
            var selected=(JObject)snapshot.DeepClone();var cooldowns=(JObject)selected["Cooldowns"];
            // A single-skill closure intentionally omits unrelated zero cooldown entries.
            foreach(var item in cooldowns.Properties().Where(p=>!imported.Project.Skills.Any(s=>s.Id==p.Name)).ToArray())item.Remove();
            if(!JToken.DeepEquals(selected,JObject.FromObject(loaded.Snapshot())))throw new Exception(kind+" export/import changed real execution.");
        }
        if(before!=SkillProjectJson.Write(source))throw new Exception("Export mutated the authoring document.");
        Console.WriteLine("PASS: project, single-skill and complete formal runtime export/import reproduce real summon/shooting execution without changing source.");
    }
}
