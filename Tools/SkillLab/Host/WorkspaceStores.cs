using Combat.Config;

namespace SkillLab;

internal sealed class WorkspaceStores
{
    public const string Arena = "arena";
    public const string Lab = "lab";
    public string Default { get; }
    readonly ProjectFileStore arena;
    readonly ProjectFileStore lab;

    public WorkspaceStores(string dataDirectory, string examples, string formal)
    {
        string state = Path.Combine(dataDirectory, "workspace-default.txt");
        string legacy = Path.Combine(dataDirectory, "project.json");
        string backup = Path.Combine(dataDirectory, "backups", "legacy-project", "project.json");
        string arenaFile = Path.Combine(dataDirectory, Arena, "project.json");
        string labFile = Path.Combine(dataDirectory, Lab, "project.json");

        Default = ProjectFileTransaction.Locked(state, () =>
        {
            string selected = Lab;
            SkillProjectDiskSnapshot migrated = null;
            if (File.Exists(state)) selected = Resolve(File.ReadAllText(state).Trim(), null);
            else if (File.Exists(backup) || File.Exists(legacy))
            {
                migrated = BackUpLegacy(legacy, backup);
                selected = migrated.Project.Id switch
                {
                    "halo-combat-runtime" => Arena,
                    "halo-skill-lab" => Lab,
                    _ when migrated.Project.Id == SkillProjectFiles.Read(examples).Id => Lab,
                    _ when migrated.Project.Id == SkillProjectFiles.Read(formal).Id => Arena,
                    _ => migrated.Project.Runtime?.Arena == null ? Lab : Arena
                };
            }

            Initialize(arenaFile, selected == Arena ? migrated : null, formal, false);
            Initialize(labFile, selected == Lab ? migrated : null, examples, true);
            if (!File.Exists(state))
                ProjectFileTransaction.Commit(new Dictionary<string, string> { [state] = selected }, []);
            return selected;
        });
        arena = new ProjectFileStore(arenaFile, Arena);
        lab = new ProjectFileStore(labFile, Lab);
    }

    public string Resolve(string requested) => Resolve(requested, Default);
    public ProjectFileStore Get(string workspace) => workspace == Arena ? arena : lab;

    static string Resolve(string requested, string fallback)
    {
        if (requested == Arena || requested == Lab) return requested;
        if (string.IsNullOrEmpty(requested) && fallback != null) return fallback;
        throw new ContentValidationException("$.workspace", "Expected arena or lab.");
    }

    static void Initialize(string path, SkillProjectDiskSnapshot migrated, string seed, bool resetPreview)
    {
        if (File.Exists(path)) return;
        var source = migrated ?? SkillProjectFiles.ReadAuthoring(seed);
        if (migrated == null && resetPreview) source.Project.Preview = SkillPreviewDefaults.Create();
        SkillProjectFiles.Write(path, source.Project, source.MissingPositions);
    }

    static SkillProjectDiskSnapshot BackUpLegacy(string legacy, string backup)
    {
        // The directory becomes visible only after every file referenced by the old index is copied.
        // A restart between backup and workspace creation resumes from the same immutable source.
        if (File.Exists(backup)) return SkillProjectFiles.ReadAuthoring(backup);
        return ProjectFileTransaction.Locked(legacy, () =>
        {
            var source = SkillProjectFiles.ReadAuthoring(legacy);
            SkillProjectStructure.Validate(source.Project);
            string destination = Path.GetDirectoryName(backup);
            string staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                string legacyDirectory = Path.GetDirectoryName(legacy);
                foreach (string file in SkillProjectFiles.ReferencedPaths(legacy).Append(legacy))
                {
                    string copy = Path.Combine(staging, Path.GetRelativePath(legacyDirectory, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(file, copy);
                }
                Directory.Move(staging, destination);
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
            return source;
        });
    }
}
