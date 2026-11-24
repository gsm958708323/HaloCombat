using Combat.Config;
using System.Security.Cryptography;
using System.Text;

namespace SkillLab;

public sealed record ProjectFileSnapshot(string Json,string ETag,bool NeedsLayout);
public sealed class ProjectVersionException(string message,int statusCode):Exception(message)
{
    public int StatusCode {get;}=statusCode;
}

public sealed class ProjectFileStore(string path,string formalPath)
{
    static ProjectFileSnapshot Snapshot(SkillProjectDiskSnapshot loaded)
    {
        string text = SkillProjectJson.Write(loaded.Project);
        // Include missing-position keys so removing an explicit (0,0) disk position also conflicts.
        var layout = loaded.MissingPositions.OrderBy(key => key.Item1, StringComparer.Ordinal)
            .ThenBy(key => key.Item2, StringComparer.Ordinal).ThenBy(key => key.Item3, StringComparer.Ordinal)
            .Select(key => new[] { key.Item1, key.Item2, key.Item3 });
        var bytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(layout) + "\n" + text);
        string etag = "\"" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + "\"";
        return new ProjectFileSnapshot(text, etag, loaded.NeedsLayout);
    }
    public ProjectFileSnapshot Read()
        => ProjectFileTransaction.Locked(path, () => Snapshot(ReadOrInitialize()));

    public ProjectFileSnapshot Save(SkillProjectDocument project, string expectedVersion)
    {
        SkillProjectStructure.Validate(project);
        return WithVersion(expectedVersion, saved =>
        {
            if (!string.Equals(project.Id, saved.Project.Id, StringComparison.Ordinal))
                throw new ContentValidationException("$.id", "保存必须保留当前工程的 ID。请通过导入合并其他工程的内容。");
            SkillProjectFiles.Write(path, project);
            return Snapshot(SkillProjectFiles.ReadAuthoring(path));
        });
    }

    internal void PublishArena(string expectedVersion)
    {
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
            var saved = ReadOrInitialize();
            if (expectedVersion != Snapshot(saved).ETag)
                throw new ProjectVersionException("磁盘工程已被其他窗口修改。当前编辑已保留，请重新载入磁盘版本。", 409);
            return update(saved);
        });
    }

    // Called under the draft lock so concurrent readers cannot initialize over a save.
    SkillProjectDiskSnapshot ReadOrInitialize()
    {
        if (!File.Exists(path))
        {
            var source = SkillProjectFiles.ReadAuthoring(formalPath);
            SkillProjectFiles.Write(path, source.Project, source.MissingPositions);
        }
        return SkillProjectFiles.ReadAuthoring(path);
    }
}
