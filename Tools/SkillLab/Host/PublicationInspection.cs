using Combat.Config;

namespace SkillLab;

public sealed record PublicationInfo(string Slot,string ProjectId,string SkillId,string Revision,string[] Skills,string[] Buffs,string[] Blueprints,string[] Timelines,string[] Projectiles,string[] Areas,string Error);
public static class PublicationInspection
{
    public static PublicationInfo Read(string directory,string slot)
    {
        var path=Path.Combine(directory,slot+".json");
        if(!File.Exists(path))return new(slot,null,null,null,[],[],[],[],[],[],null);
        try
        {
            var publication=SkillPublicationJson.Read(File.ReadAllText(path)).Document;
            return new(slot,publication.Project.Id,publication.SkillId,publication.Revision,
                publication.Project.Skills.Select(s=>s.Id).ToArray(),publication.Project.Buffs.Select(b=>b.Id).ToArray(),
                publication.Project.SummonBlueprints.Select(b=>b.Id).ToArray(),publication.Project.Timelines.Select(t=>t.Id).ToArray(),publication.Project.Projectiles.Select(p=>p.Id).ToArray(),publication.Project.Areas.Select(a=>a.Id).ToArray(),null);
        }
        catch(Exception error) when(error is ContentValidationException or IOException or UnauthorizedAccessException)
        {return new(slot,null,null,null,[],[],[],[],[],[],error.Message);}
    }
}
