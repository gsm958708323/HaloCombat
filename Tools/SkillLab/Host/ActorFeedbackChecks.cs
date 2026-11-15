using Combat.Config;

namespace SkillLab;

internal static class ActorFeedbackChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));p.Preview.Caster.X=0;
        p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Hp=1000}};p.Preview.Recovery=0;
        using var s=new SkillSimulation(p,CombatContentCompiler.Compile(p));
        if(!s.Cast("actor-hitstop",2,0).Accepted)throw new Exception("Actor feedback cast rejected.");s.Advance(1);s.Move(1,0);s.Advance(1);
        var stopped=s.Snapshot();var caster=stopped.Actors.Single(a=>a.Kind=="caster");var target=stopped.Actors.Single(a=>a.Kind=="target");
        if(!caster.Stopped||!target.Stopped||caster.X!=0||caster.HitstopFrames!=1||target.HitstopFrames!=3||target.Hp!=990)throw new Exception("Independent actor stop mismatch.");
        if(!stopped.Events.Any(e=>e.Kind=="hurt"&&e.Result=="heavy"))throw new Exception("Actual graded hurt event missing.");
        s.Advance(2);var moving=s.Snapshot();if(moving.Actors.Single(a=>a.Kind=="caster").Stopped||!moving.Actors.Single(a=>a.Kind=="target").Stopped)throw new Exception("Actor stop durations were coupled.");
        Console.WriteLine("PASS: independent next-step2/4 actor stops, actual10 HP damage, Heavy hurt event and separate recovery.");
    }
}
