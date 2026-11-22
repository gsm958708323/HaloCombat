using Combat.Config;

namespace SkillLab;

internal static class PublicationSyncChecks
{
    private static readonly string[] Roots = ["basic-shot", "summon-gunners", "melee-fan", "melee-whirl"];

    public static void Run(string root)
    {
        var project = SkillProjectFiles.Read(Path.Combine(root, "Content/SkillLab/project.json"));
        using var session = new PublishedSkillSession();
        var directory = Directory.CreateTempSubdirectory("halo-publication-slots-");
        string path = directory.FullName;
        try
        {
            var names = PublishedSkillSlots.Ordered;
            Require(names.SequenceEqual(new[] { "f6", "f7", "slot3", "slot4" }), "Publication filenames or order changed.");
            ExpectContentError(() => PublicationFiles.Sync(path, project), "$.slots");
            Require(PublicationFiles.Inspect(path).All(slot => slot.SkillId == null), "Empty slots must be listed.");

            // Populate in reverse order to verify the merge and inspection use slot order.
            foreach (int index in Enumerable.Range(0, names.Count).Reverse())
            {
                var publication = SkillPublicationJson.Create(project, Roots[index]);
                Require(publication.FormatVersion == 1, "Portable publication version changed.");
                Require(PublicationFiles.Publish(path, names[index], publication) == null, "Same-project publications should be compatible.");
                if (index == 3)
                {
                    var sparse = PublicationFiles.Sync(path, project);
                    Require(sparse.Count == 1 && sparse.ContainsKey(names[index]), "Sync filled empty slots.");
                    session.Replace(new Dictionary<string, string> { [names[index]] = SkillPublicationJson.Write(publication) });
                    Require(session.Slots.Single().Value == Roots[index], "A lone slot 4 should load.");
                }
            }
            var before = Paths(names, path).Select(File.ReadAllText).ToArray();
            var read = PublishedSkillSlots.Read(path + Path.DirectorySeparatorChar);
            Require(names.Select(name => read[name]).SequenceEqual(before), "Shared slot reader did not return all four files.");
            var reversed = names.Zip(before).Reverse().ToDictionary(pair => pair.First, pair => pair.Second);
            session.Replace(reversed);
            Require(names.Select(name => session.Slots[name]).SequenceEqual(Roots), "Four-slot load lost selected roots.");
            string revision = session.Revision;
            session.Replace(names.Zip(before).ToDictionary(pair => pair.First, pair => pair.Second));
            Require(session.Revision == revision, "Combined revision depends on input order.");
            ExpectContentError(() => session.Replace(new Dictionary<string, string> { ["slot5"] = before[0] }), "$.slots");
            Require(session.Revision == revision, "Rejected slot replaced the working session.");
            VerifyInspection(path, names);

            project.Preview.Caster.X += 0.125f;
            if (OperatingSystem.IsWindows())
            {
                // The plan can read slot 4, but Windows rejects replacement while it is open without FileShare.Delete.
                using (File.Open(Paths(names, path).Last(), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try { PublicationFiles.Sync(path, project); throw new Exception("Sync should fail on the locked fourth slot."); }
                    catch (IOException) { }
                }
                Require(Paths(names, path).Select(File.ReadAllText).SequenceEqual(before), "Sync failed to roll back previous writes.");
                Require(!Directory.EnumerateFiles(path, "*.tmp").Any(), "A failed sync left a temporary file.");
            }

            var plan = PublicationFiles.Sync(path, project);
            Require(plan.Count == 4 && names.Select(name => plan[name].SkillId).SequenceEqual(Roots), "Sync lost occupied roots.");
            var synced = Paths(names, path).Select(File.ReadAllText).ToArray();
            Require(synced.Zip(before).All(pair => pair.First != pair.Second), "Sync did not rewrite all occupied slots.");
            var foreign = SkillProjectJson.Read(SkillProjectJson.Write(project));
            foreign.Id = "other-project";
            ExpectContentError(() => PublicationFiles.Sync(path, foreign), "$.slots.f6");
            Require(Paths(names, path).Select(File.ReadAllText).SequenceEqual(synced), "Rejected sync wrote slot files.");

            // Single publication permits explicit replacement and reports every remaining foreign slot.
            var warning = PublicationFiles.Publish(path, names[0], SkillPublicationJson.Create(foreign, Roots[0]));
            Require(warning?.Contains("槽位 2、3、4") == true, "Mixed-source warning omitted foreign slots.");
            Require(Paths(names, path).Skip(1).Select(File.ReadAllText).SequenceEqual(synced.Skip(1)), "Single publish changed another slot.");
            for (int index = 1; index < names.Count; index++)
                warning = PublicationFiles.Publish(path, names[index], SkillPublicationJson.Create(foreign, Roots[index]));
            Require(warning == null, "Republishing the remaining slots did not resolve mixed sources.");
            VerifyInspection(path, names);
        }
        finally { directory.Delete(true); }
        Console.WriteLine("PASS: four-slot inspection, sparse/ordered load, sync, mixed-source replacement and Windows disk rollback.");
    }

    private static IEnumerable<string> Paths(IEnumerable<string> names, string directory) =>
        names.Select(name => Path.Combine(directory, name + ".json"));

    private static void VerifyInspection(string path, IReadOnlyList<string> names)
    {
        var inspection = PublicationFiles.Inspect(path);
        Require(inspection.Select(slot => slot.Slot).SequenceEqual(names), "Inspection returned slots out of order.");
        Require(inspection.Select(slot => slot.SkillId).SequenceEqual(Roots), "Inspection lost selected roots.");
    }

    private static void ExpectContentError(Action operation, string path)
    {
        try { operation(); }
        catch (ContentValidationException error) when (error.Path == path) { return; }
        throw new Exception("Expected slot validation failure at " + path);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
