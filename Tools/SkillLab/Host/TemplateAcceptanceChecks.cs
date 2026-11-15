using Combat.Config;

namespace SkillLab;

internal static class TemplateAcceptanceChecks
{
    public static void Run(string root)
    {
        var source=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));
        foreach(var skill in source.Skills){
            var p=SkillProjectJson.SelectTemplate(source,skill.Id);
            var exported=SkillJsonTransfer.Export(p,"skill",skill.Id);var imported=SkillJsonTransfer.Import(exported.Json).Project;
            using var simulation=new SkillSimulation(imported,CombatContentCompiler.Compile(imported));
            simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=skill.Id,X=0,Z=0});
            System.Text.Json.JsonSerializer.Serialize(simulation.Snapshot());
            simulation.Advance(120);System.Text.Json.JsonSerializer.Serialize(simulation.Snapshot());
            simulation.Execute(new SkillSimulationCommand{Kind="release"});simulation.Advance(180);
            System.Text.Json.JsonSerializer.Serialize(simulation.Snapshot());
            using var replay=new SkillReplayRun(simulation.CaptureReplay(skill.Id));
            if(!replay.Comparison.Matches)throw new Exception("Template replay differs: "+skill.Id+" / "+replay.Comparison.Differences[0].Path);
        }
        Console.WriteLine("PASS: all "+source.Skills.Length+" template publication/import closures, actual cast/release/300 steps, strict HTTP JSON number serialization and full replay.");
    }
}
