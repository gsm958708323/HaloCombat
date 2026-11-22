using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    internal static class SkillLabDefaultContent
    {
        public static IReadOnlyDictionary<string, CompiledSkillPublication> Create(RuntimeProjectLoader loader)
        {
            var runtime = loader.Content.Runtime;
            var arena = runtime.Arena;
            if (arena == null)
                throw new ContentValidationException("$.runtime.arena", "SkillLab defaults require Arena start bindings.");

            // Use the same idle actor tags and combo priority as a newly spawned Arena player.
            var tags = new TagComp();
            var groundTag = arena.PlayerPosition.Y <= runtime.Data.Motor.GroundY + .0001f
                ? CommonTags.Grounded : CommonTags.Airborne;
            tags.Add(groundTag, 1, TagSource.Effect("SkillLab default bindings"));
            var project = SkillProjectJson.Read(SkillProjectJson.Write(loader.Project));
            project.Preview = SkillPreviewDefaults.Create();
            var result = new Dictionary<string, CompiledSkillPublication>(StringComparer.Ordinal);
            for (int i = 0; i < PublishedSkillSlots.Ordered.Count; i++)
            {
                var action = "Fire" + (i + 1);
                var binding = arena.Bindings.FirstOrDefault(value => value.Action == action);
                if (!binding.Input.IsValid || !runtime.Data.Combo.TryResolve(default, binding.Input, tags, out var starter)
                    || !loader.Content.Skills.TryGet(starter.ToSkill, out var skill)) continue;

                // Reuse the publication dependency closure and revision. This entry only
                // exists in memory and never occupies a published slot on disk.
                var publication = SkillPublicationJson.Create(project, skill.Key);
                result.Add(PublishedSkillSlots.Ordered[i],
                    new CompiledSkillPublication(publication, CombatContentCompiler.Compile(publication.Project)));
            }
            if (result.Count == 0)
                throw new ContentValidationException("$.runtime.arena.bindings", "No idle Fire1–Fire4 start skills are available.");
            return result;
        }
    }
}
