using Combat.Config;
using System.Security.Cryptography;
using System.Text;

namespace SkillLab;

public sealed record ProjectFileSnapshot(string Json,string ETag);
public sealed class ProjectVersionException(string message,int statusCode):Exception(message)
{
    public int StatusCode {get;}=statusCode;
}

public sealed class ProjectFileStore(string path)
{
    readonly SemaphoreSlim gate=new(1);
    static string ETag(string text)=>"\""+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()+"\"";
    public async Task<ProjectFileSnapshot> Read()
    {
        await gate.WaitAsync();
        try {var text=await File.ReadAllTextAsync(path);return new(text,ETag(text));}
        finally {gate.Release();}
    }
    public async Task<ProjectFileSnapshot> Save(SkillProjectDocument project,string expectedVersion)
    {
        SkillProjectStructure.Validate(project);
        if(string.IsNullOrWhiteSpace(expectedVersion))throw new ProjectVersionException("保存缺少磁盘版本，请先重新载入工程。",428);
        var text=SkillProjectJson.Write(project);
        await gate.WaitAsync();
        string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            string existing=await File.ReadAllTextAsync(path);
            if(expectedVersion!=ETag(existing))throw new ProjectVersionException("磁盘工程已被其他窗口修改。当前编辑已保留；请先导出，或重新载入磁盘版本后再编辑。",409);
            await File.WriteAllTextAsync(temporary,text);
            File.Move(temporary,path,true);
            return new(text,ETag(text));
        }
        finally {try{if(File.Exists(temporary))File.Delete(temporary);}finally{gate.Release();}}
    }
}

public sealed record PublicationInfo(string Slot,string ProjectId,string SkillId,string Revision,string[] Skills,string[] Buffs,string[] Blueprints,string Error);
public static class PublicationInspection
{
    public static PublicationInfo Read(string directory,string slot)
    {
        var path=Path.Combine(directory,slot+".json");
        if(!File.Exists(path))return new(slot,null,null,null,[],[],[],null);
        try
        {
            var publication=SkillPublicationJson.Read(File.ReadAllText(path)).Document;
            return new(slot,publication.Project.Id,publication.SkillId,publication.Revision,
                publication.Project.Skills.Select(s=>s.Id).ToArray(),publication.Project.Buffs.Select(b=>b.Id).ToArray(),
                publication.Project.SummonBlueprints.Select(b=>b.Id).ToArray(),null);
        }
        catch(Exception error) when(error is ContentValidationException or IOException or UnauthorizedAccessException)
        {return new(slot,null,null,null,[],[],[],error.Message);}
    }
}
