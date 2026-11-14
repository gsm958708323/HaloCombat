using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class AmmoRecastChecks
{
    public static void Run(string root)
    {
        var project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        project.Preview.Caster.X=0;project.Preview.Targets=Array.Empty<PreviewActorDocument>();project.Preview.Recovery=0;project.Preview.InitialAmmo=0;
        using(var s=new SkillSimulation(project,CombatContentCompiler.Compile(project)))
        {
            Check(s.Cast("ammo-shot",20,0).Accepted,"Missing-ammo fallback failed.");s.Advance(54);Check(s.Snapshot().Ammo==0,"Reload completed too early.");
            s.Advance(1);Check(s.Snapshot().Ammo==60,"Authored 1.1 second reload payload did not refill.");s.Advance(3);Check(s.Cast("ammo-shot",20,0).Accepted,"Loaded shot rejected.");Check(s.Snapshot().Ammo==59,"Actual ammunition cost missing.");
        }
        project.Preview.InitialAmmo=null;
        var publication=SkillPublicationJson.Create(project,"tracked-shot");Check(publication.Project.Skills.Length==2,"Recast closure omitted its operation skill.");
        using(var first=new SkillSimulation(project,CombatContentCompiler.Compile(project)))using(var replay=new SkillSimulation(project,CombatContentCompiler.Compile(project)))
        {
            foreach(var s in new[]{first,replay})
            {
                s.Cast("tracked-shot",20,0);s.Advance(26);float point=s.Snapshot().Actors.Single(a=>a.Kind=="projectile").X;
                Check(s.Cast("tracked-shot",20,0).Accepted,"Live projectile did not resolve the recast.");s.Advance(1);
                Check(Math.Abs(point-s.Snapshot().Actors.Single(a=>a.Kind=="caster").X)<.0001,"Warp did not use the captured actual point.");Check(!s.Snapshot().Actors.Any(a=>a.Kind=="projectile"),"Warped body remained active.");
            }
            Check(JToken.DeepEquals(JObject.FromObject(first.Snapshot()),JObject.FromObject(replay.Snapshot())),"Ammo/recast deterministic replay diverged.");
        }
        Console.WriteLine("PASS: delayed authored reload, actual ammunition payment, published recast dependency and full deterministic teleport replay.");
        var formal=new RuntimeProjectLoader(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json"));
        formal.Project.Preview.Caster.X=0;formal.Project.Preview.Targets=Array.Empty<PreviewActorDocument>();
        using(var s=new SkillSimulation(formal.Project,formal.Content))
        {
            Check(s.Cast("arena-tracked-shot",20,0).Accepted,"Formal tracked shot rejected.");s.Advance(26);
            var body=s.Snapshot().Actors.Single(a=>a.Kind=="projectile");Check(body.X>.4f&&body.X<1,"Formal Arena lost its gradual historical acceleration curve.");
            Console.WriteLine("PASS: formal Arena ramp age="+(3-body.Remaining)+" / projectile x="+body.X+" / speed="+body.Speed);
        }
    }
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
}
