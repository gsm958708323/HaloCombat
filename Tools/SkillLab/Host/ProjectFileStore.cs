using Combat.Config;
using System.Security.Cryptography;
using System.Text;

namespace SkillLab;

public sealed record ProjectFileSnapshot(string Json,string ETag,bool NeedsLayout);
public sealed class ProjectVersionException(string message,int statusCode):Exception(message)
{
    public int StatusCode {get;}=statusCode;
}

public sealed class ProjectFileStore(string path,string workspace="")
{
    readonly SemaphoreSlim gate=new(1);
    ProjectFileSnapshot Snapshot(SkillProjectDiskSnapshot loaded)
    {
        string text = SkillProjectJson.Write(loaded.Project);
        // Include missing-position keys so removing an explicit (0,0) disk position also conflicts.
        var layout = loaded.MissingPositions.OrderBy(key => key.Item1, StringComparer.Ordinal)
            .ThenBy(key => key.Item2, StringComparer.Ordinal).ThenBy(key => key.Item3, StringComparer.Ordinal)
            .Select(key => new[] { key.Item1, key.Item2, key.Item3 });
        var bytes = Encoding.UTF8.GetBytes(workspace + "\n" + System.Text.Json.JsonSerializer.Serialize(layout) + "\n" + text);
        string etag = "\"" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + "\"";
        return new ProjectFileSnapshot(text, etag, loaded.NeedsLayout);
    }
    public async Task<ProjectFileSnapshot> Read()
    {
        await gate.WaitAsync();
        try {return Snapshot(SkillProjectFiles.ReadAuthoring(path));}
        finally {gate.Release();}
    }
    internal SkillJsonImport PrepareCopy(string skillId)
        => CrossWorkspaceCopy.Prepare(SkillProjectFiles.ReadAuthoring(path), skillId);

    public async Task<ProjectFileSnapshot> Save(SkillProjectDocument project,string expectedVersion)
    {
        SkillProjectStructure.Validate(project);
        await gate.WaitAsync();
        try
        {
            return WithVersion(expectedVersion, _ =>
            {
                SkillProjectFiles.Write(path, project);
                return Snapshot(SkillProjectFiles.ReadAuthoring(path));
            });
        }
        finally {gate.Release();}
    }

    internal void PublishArena(string expectedVersion, string formalPath)
    {
        if (workspace != WorkspaceStores.Arena)
            throw new ProjectVersionException("只有 Arena 工作区可以发布正式内容。", 403);
        WithVersion(expectedVersion, saved =>
        {
            var project = saved.Project;
            var compiled = CombatContentCompiler.Compile(project);
            if (compiled.Runtime == null || project.Runtime.Actors.Length == 0)
                throw new ContentValidationException("$.runtime.actors", "正式内容需要角色定义。");
            if (compiled.Runtime.Arena == null)
                throw new ContentValidationException("$.runtime.arena", "正式内容需要 Arena 定义。");
            project.Editor = null;
            SkillProjectFiles.Write(formalPath, project, saved.MissingPositions);
            return true;
        });
    }

    T WithVersion<T>(string expectedVersion, Func<SkillProjectDiskSnapshot, T> update)
    {
        if (string.IsNullOrWhiteSpace(expectedVersion))
            throw new ProjectVersionException("缺少草稿版本，请先重新载入工程。", 428);
        return ProjectFileTransaction.Locked(path, () =>
        {
            var saved = SkillProjectFiles.ReadAuthoring(path);
            if (expectedVersion != Snapshot(saved).ETag)
                throw new ProjectVersionException("磁盘工程已被其他窗口修改。当前编辑已保留，请重新载入磁盘版本。", 409);
            return update(saved);
        });
    }
}

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
