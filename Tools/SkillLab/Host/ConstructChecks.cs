using Combat.Config;

namespace SkillLab;

internal static class ConstructChecks
{
    public static void Run(string root)
    {
        var project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        project.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=0,Hp=1000}};
        var graph=project.Skills.Single(s=>s.Id=="barrel-chain");graph.Nodes.Single(n=>n.Kind=="cast").Parameters["delay"]=0;
        graph.Nodes.Single(n=>n.Kind=="damage").Parameters["canCrit"]=false;graph.Nodes.Single(n=>n.Kind=="construct").Parameters["decayPeriod"]=0;
        using var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project));
        Check(simulation.Cast("barrel-chain",0,0).Accepted,"Construct cast failed.");simulation.Advance(25);
        var objects=simulation.Snapshot().Actors.Where(a=>a.Kind=="construct").ToArray();
        Check(objects.Length==3&&objects.All(a=>a.Hp==5&&a.Owner=="caster"&&a.Group=="barrel"),"Construct setup mismatch.");
        Check(simulation.Cast("detonate-constructs",0,0).Accepted,"Detonation cast failed.");simulation.Advance(1);
        var result=simulation.Snapshot();Check(!result.Actors.Any(a=>a.Kind=="construct"),"Death wave left live objects.");
        Check(result.Actors.Single(a=>a.Kind=="target").Hp==964,"Three actual object blasts did not deal36 damage.");
        Check(result.Statistics.Damage==51,"Expected15 actual construct HP damage plus36 enemy HP damage.");
        Console.WriteLine("PASS: three configured destructible bodies, actual health cap, FIFO death chain and36 enemy damage with full cleanup.");
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
