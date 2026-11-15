using Combat.Config;

namespace SkillLab;

internal static class ReplayChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        p.Preview.Seed=753;
        using var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p));
        simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="held-spray",X=0,Z=0});
        simulation.Execute(new SkillSimulationCommand{Kind="move",X=0,Z=1});simulation.Advance(12);
        simulation.Execute(new SkillSimulationCommand{Kind="hitstop",Frames=3});simulation.Advance(8);
        simulation.Execute(new SkillSimulationCommand{Kind="release"});simulation.Execute(new SkillSimulationCommand{Kind="cancel"});simulation.Advance(60);
        var file=SkillReplayJson.Read(SkillReplayJson.Write(simulation.CaptureReplay("held-spray")));
        using var run=new SkillReplayRun(file);
        if(!run.Comparison.Matches)throw new Exception("Replay state differs: "+run.Comparison.Differences[0].Path);
        file.ExpectedState["energy"]=-100;
        using var bad=new SkillReplayRun(file);
        if(bad.Comparison.Matches)throw new Exception("Replay failed to detect a mismatching expected result.");
        Console.WriteLine("PASS: recorded input order, fixed clock, seeded channel/movement/hitstop/release and whole snapshot replay; mismatching results rejected.");
    }
}
