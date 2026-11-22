using Combat.Config;

namespace SkillLab;

internal static class EffectNodeMigration
{
    public static void Run(HostPaths paths)
    {
        var sources = new[]
        {
            (Group: "draft", Path: paths.DraftProject),
            (Group: "formal", Path: paths.FormalProject),
            (Group: "examples", Path: paths.ExampleProject)
        };
        string backups = Path.Combine(paths.Data, "backups");
        string backup = Path.Combine(backups, $"effect-nodes-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");

        // The Host also acquires the draft before the formal project when seeding or publishing.
        bool LockSources(int index) => index < sources.Length
            ? ProjectFileTransaction.Locked(sources[index].Path, () => LockSources(index + 1))
            : Migrate();

        bool Migrate()
        {
            if (Directory.Exists(backups) && (File.GetAttributes(backups) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Backup directory must be inside the selected data directory: " + backups);

            var changes = new List<(string Path, SkillProjectDiskSnapshot Snapshot)>();
            foreach (var (group, path) in sources)
            {
                if (!File.Exists(path)) continue;
                var snapshot = SkillProjectFiles.ReadAuthoring(path);
                SkillProjectStructure.Validate(snapshot.Project);
                CombatContentCompiler.Compile(snapshot.Project);
                BackupProject(path, Path.Combine(backup, group));
                changes.Add((path, snapshot));
            }

            // Validate and back up all source files before replacing any project.
            foreach (var (path, snapshot) in changes)
            {
                SkillProjectFiles.Write(path, snapshot.Project, snapshot.MissingPositions);
                var migrated = SkillProjectFiles.ReadAuthoring(path);
                CombatContentCompiler.Compile(migrated.Project);
                if (SkillProjectJson.Write(migrated.Project) != SkillProjectJson.Write(snapshot.Project) ||
                    !migrated.MissingPositions.SetEquals(snapshot.MissingPositions))
                    throw new InvalidOperationException("Migration roundtrip differed: " + path);
                Console.WriteLine($"Migrated {path}: {migrated.Project.Skills.Length} skills");
            }
            Console.WriteLine("Original content backup: " + backup);
            return true;
        }

        LockSources(0);
    }

    static void BackupProject(string index, string destinationDirectory)
    {
        string sourceDirectory = Path.GetDirectoryName(index)!;
        foreach (string file in SkillProjectFiles.ReferencedPaths(index).Append(index))
        {
            string relative = Path.GetRelativePath(sourceDirectory, file);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new IOException("Content file leaves its project directory: " + file);
            string destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}
