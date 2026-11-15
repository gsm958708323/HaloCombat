using Combat.Config;

namespace SkillLab;

internal static class AuthorTraceChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        var publication=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,"burn-shot")));
        using var simulation=new SkillSimulation(publication.Document.Project,publication.Content);
        simulation.Cast("burn-shot",0,0);simulation.Advance(200);var state=simulation.Snapshot();
        var damage=state.Events.Where(e=>e.Kind=="damage").ToArray();
        if(damage.Length==0||damage.Any(e=>e.Node?.GraphKind!="buff"||e.Skill!="burn-shot"||e.TargetId==null||e.SourceId==null))throw new Exception("Delayed damage lost exact author/entity provenance.");
        if(!state.Events.Any(e=>e.Kind=="cleanup"&&e.Node?.NodeKind=="projectile"&&e.BodyId!=null))throw new Exception("Recycled projectile lost its complete identity and definition source.");
        Console.WriteLine("PASS: published delayed Buff damage and projectile cleanup retain actual author graph/node, cast and full entity identities.");
    }
}
