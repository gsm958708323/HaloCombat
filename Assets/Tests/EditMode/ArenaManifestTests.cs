using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ArenaManifestTests
    {
        [Test]
        public void DuplicateSkillIdsAreRejected()
        {
            var manifest = ScriptableObject.CreateInstance<ArenaContentManifestAsset>();
            var first = ScriptableObject.CreateInstance<BuffArenaSkillAsset>();
            var second = ScriptableObject.CreateInstance<BuffArenaSkillAsset>();
            first.SkillIdValue = second.SkillIdValue = 2001;
            try
            {
                manifest.Skills = new[] { first, second };
                Assert.IsFalse(manifest.ValidateIds(out var error));
                StringAssert.Contains("duplicate skill id 2001", error);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SkillTimelineReferenceMustBeAuthoritative()
        {
            var manifest = ScriptableObject.CreateInstance<ArenaContentManifestAsset>();
            var skill = ScriptableObject.CreateInstance<BuffArenaSkillAsset>();
            var timeline = ScriptableObject.CreateInstance<SkillTimelineAsset>();
            var cues = ScriptableObject.CreateInstance<CueLibraryAsset>();
            try
            {
                manifest.Cues = cues;
                skill.SkillIdValue = 2001;
                skill.Timeline = timeline;
                timeline.TimelineIdValue = 3001;
                manifest.Skills = new[] { skill };
                manifest.Timelines = new[] { timeline };
                Assert.IsTrue(manifest.ValidateContent(out var error), error);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(cues);
            }
        }
    }
}
