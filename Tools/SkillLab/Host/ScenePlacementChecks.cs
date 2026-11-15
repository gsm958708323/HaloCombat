using Combat.Config;

namespace SkillLab;

internal static class ScenePlacementChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        using var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p));
        simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0});simulation.Advance(10);
        var state=simulation.Snapshot();var target=state.Actors.First(a=>a.Kind=="target");var shot=state.Actors.Single(a=>a.Kind=="projectile");
        simulation.Execute(new SkillSimulationCommand{Kind="place",ActorId=target.SceneId,EntityId=target.Id,X=2,Z=2});
        var after=simulation.Snapshot();
        if(after.Step!=10||after.Actors.Single(a=>a.Id==target.Id).Z!=2||after.Actors.Single(a=>a.Id==shot.Id).X!=shot.X)throw new Exception("Placement advanced or replaced the running preview.");
        using var replay=new SkillReplayRun(simulation.CaptureReplay("basic-shot"));if(!replay.Comparison.Matches)throw new Exception("Placement input did not reproduce its paused state.");
        Console.WriteLine("PASS: exact scene/entity placement updates the paused pose, preserves time/projectile identity and reproduces from the original configuration.");
    }
}
