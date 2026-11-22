using Combat.Config;

namespace SkillLab;

internal sealed record InvalidPublicationSlot(string Slot, string Reason);
internal sealed record PublicationConflict(string Error, string GroupRevision, IReadOnlyList<InvalidPublicationSlot> InvalidSlots);
internal sealed record PublicationResult(Dictionary<string, SkillPublicationDocument> Published, PublicationConflict Conflict);

internal static class PublicationFiles
{
    public static PublicationInfo[] Inspect(string directory) => PublishedSkillSlots.Locked(directory, () =>
        PublishedSkillSlots.Ordered.Select(slot => PublicationInspection.Read(directory, slot)).ToArray());

    public static PublicationResult Publish(string directory, string selected, SkillProjectDocument project, string skillId,
        string expectedGroup, IReadOnlyList<string> clearInvalidSlots) => PublishedSkillSlots.Locked<PublicationResult>(directory, () =>
    {
        SkillProjectStructure.ValidateRuntimeStructure(project);
        var current = PublishedSkillSlots.ReadUnlocked(directory);
        var roots = new Dictionary<string, string> { [selected] = skillId };
        var invalid = new List<InvalidPublicationSlot>();
        var available = project.Skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var (slot, text) in current)
        {
            if (slot == selected) continue;
            try
            {
                var prior = SkillPublicationJson.Read(text).Document;
                if (prior.Project.Id != project.Id)
                    invalid.Add(new(slot, "快照来自其他工程。"));
                else if (!available.Contains(prior.SkillId))
                    invalid.Add(new(slot, $"当前工程已没有技能 {prior.SkillId}。"));
                else
                    roots.Add(slot, prior.SkillId);
            }
            catch (ContentValidationException error)
            {
                invalid.Add(new(slot, "快照无法载入。" + error.Message));
            }
        }

        // Fingerprint the entire observed group, including malformed files and empty slots.
        // The confirmation grants permission to remove exactly the invalid slots from this observation.
        string groupRevision = "\"" + SkillJsonCodec.Fingerprint(SkillJsonCodec.Write(current)) + "\"";
        bool confirming = !string.IsNullOrEmpty(expectedGroup) || clearInvalidSlots.Count > 0;
        if (confirming && expectedGroup != groupRevision)
            return new(null, new("槽位已发生变化，请检查当前槽位后重新发布。", groupRevision, invalid));
        var invalidNames = invalid.Select(entry => entry.Slot).ToHashSet(StringComparer.Ordinal);
        if (clearInvalidSlots.Count != invalid.Count || !invalidNames.SetEquals(clearInvalidSlots))
            return new(null, new("清空下列失效槽位后才能发布。", groupRevision, invalid));

        var plan = new Dictionary<string, SkillPublicationDocument>();
        foreach (var (slot, root) in roots)
        {
            try { plan.Add(slot, SkillPublicationJson.Create(project, root)); }
            catch (ContentValidationException error)
            {
                throw new ContentValidationException("$.slots." + slot, $"槽位 {Number(slot)} 发布校验失败。{error.Message}");
            }
        }
        var output = plan.ToDictionary(pair => pair.Key, pair => SkillPublicationJson.Write(pair.Value));
        using var session = new PublishedSkillSession();
        session.Replace(output);
        ProjectFileTransaction.Commit(
            output.ToDictionary(pair => PublishedSkillSlots.PathOf(directory, pair.Key), pair => pair.Value),
            [], invalid.Select(entry => PublishedSkillSlots.PathOf(directory, entry.Slot)));
        return new(plan, null);
    });

    public static int Number(string slot) => Enumerable.Range(0, PublishedSkillSlots.Ordered.Count)
        .First(index => PublishedSkillSlots.Ordered[index] == slot) + 1;
}
