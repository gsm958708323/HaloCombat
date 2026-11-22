using Combat.Config;

namespace SkillLab;

internal static class PublicationFiles
{
    public static PublicationInfo[] Inspect(string directory) => PublishedSkillSlots.Locked(directory, () =>
        PublishedSkillSlots.Ordered.Select(slot => PublicationInspection.Read(directory, slot)).ToArray());

    public static Dictionary<string, SkillPublicationDocument> Sync(string directory, SkillProjectDocument project)
    {
        return PublishedSkillSlots.Locked(directory, () =>
        {
            var plan = PlanSync(project, PublishedSkillSlots.ReadUnlocked(directory));
            ProjectFileTransaction.Commit(plan.ToDictionary(
                pair => SlotPath(directory, pair.Key), pair => SkillPublicationJson.Write(pair.Value)), []);
            return plan;
        });
    }

    public static string Publish(string directory, string slot, SkillPublicationDocument publication)
    {
        if (!PublishedSkillSlots.Ordered.Contains(slot))
            throw new ContentValidationException("$.slots", "Unknown slot: " + slot);
        return PublishedSkillSlots.Locked(directory, () =>
        {
            ProjectFileTransaction.Commit(new Dictionary<string, string>
                { [SlotPath(directory, slot)] = SkillPublicationJson.Write(publication) }, []);
            return LoadWarning(directory, publication.Project.Id);
        });
    }

    public static Dictionary<string, SkillPublicationDocument> PlanSync(
        SkillProjectDocument project, IReadOnlyDictionary<string, string> current)
    {
        if (current.Count == 0)
            throw new ContentValidationException("$.slots", "请先发布至少一个技能槽，再同步现有槽位。");
        var result = new Dictionary<string, SkillPublicationDocument>();
        foreach (var pair in current)
        {
            var original = ReadPublication(pair.Key, pair.Value);
            if (original.Project.Id != project.Id)
                throw new ContentValidationException("$.slots." + pair.Key,
                    $"槽位 {Number(pair.Key)} 来自其他工程。请为该槽选择技能并重新发布，再同步现有槽位。");
            result.Add(pair.Key, SkillPublicationJson.Create(project, original.SkillId));
        }
        using var session = new PublishedSkillSession();
        session.Replace(result.ToDictionary(pair => pair.Key, pair => SkillPublicationJson.Write(pair.Value)));
        return result;
    }

    public static int Number(string slot) => Enumerable.Range(0, PublishedSkillSlots.Ordered.Count)
        .First(index => PublishedSkillSlots.Ordered[index] == slot) + 1;

    public static string SlotPath(string directory, string slot) => PublishedSkillSlots.PathOf(directory, slot);

    private static string LoadWarning(string directory, string projectId)
    {
        try
        {
            var slots = PublishedSkillSlots.ReadUnlocked(directory);
            var foreign = new List<int>();
            var unreadable = new List<int>();
            foreach (var pair in slots)
            {
                try
                {
                    if (ReadPublication(pair.Key, pair.Value).Project.Id != projectId) foreign.Add(Number(pair.Key));
                }
                catch (ContentValidationException) { unreadable.Add(Number(pair.Key)); }
            }
            var warnings = new List<string>();
            if (foreign.Count > 0)
                warnings.Add($"发布槽位来自不同工程。请为槽位 {string.Join("、", foreign)} 选择当前工程的技能并重新发布。");
            if (unreadable.Count > 0)
                warnings.Add($"槽位 {string.Join("、", unreadable)} 的快照无法读取，请重新发布。");
            if (warnings.Count > 0) return string.Join(" ", warnings);

            using var session = new PublishedSkillSession();
            session.Replace(slots);
            return null;
        }
        catch (ContentValidationException error)
        {
            return "当前发布槽位暂不能共同载入。" + error.Message + "。同工程共享内容修改后，请点击“同步现有槽位”。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Publication is already committed. Report the inspection failure as a warning.
            return "已发布，暂无法检查其他发布槽位。" + error.Message;
        }
    }

    private static SkillPublicationDocument ReadPublication(string slot, string text)
    {
        try { return SkillPublicationJson.Read(text).Document; }
        catch (ContentValidationException error)
        { throw new ContentValidationException("$.slots." + slot, error.Message); }
    }

}
