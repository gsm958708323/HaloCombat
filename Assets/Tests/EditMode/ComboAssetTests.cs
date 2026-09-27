using System;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ComboAssetTests
    {
        BuffArenaSkillAsset a, b;
        ComboTableAsset table;
        [SetUp] public void Setup()
        {
            a = ScriptableObject.CreateInstance<BuffArenaSkillAsset>();
            b = ScriptableObject.CreateInstance<BuffArenaSkillAsset>();
            table = ScriptableObject.CreateInstance<ComboTableAsset>();
            a.SkillIdValue = 1; a.Timeline = ScriptableObject.CreateInstance<SkillTimelineAsset>(); a.Timeline.TimelineIdValue = 11;
            b.SkillIdValue = 2; b.Timeline = ScriptableObject.CreateInstance<SkillTimelineAsset>(); b.Timeline.TimelineIdValue = 12;
        }
        [TearDown] public void Cleanup()
        { UnityEngine.Object.DestroyImmediate(table); UnityEngine.Object.DestroyImmediate(a.Timeline); UnityEngine.Object.DestroyImmediate(b.Timeline); UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        ComboEntryAsset Link(int priority = 10) => new ComboEntryAsset {
            PreSkillAssets = new[] { a }, InputAction = "Fire1", RequiredTags = new[] { CommonTags.Cancel.Value }, Priority = priority, Skill = b };
        [Test] public void ExplicitLinksAndStartsUseTargetTimeline()
        {
            table.Entries = new[] { Link(), new ComboEntryAsset { Skill = a, InputAction = "Fire1" } };
            var baked = table.Bake(new[] { a, b });
            Assert.AreEqual(2, baked.Entries.Length);
            var tags = new TagComp();
            Assert.IsTrue(baked.TryResolve(SkillNodeId.None, new InputToken("Fire1"), tags, out var start));
            Assert.AreEqual(1, start.ToSkill.Value);
            Assert.IsFalse(baked.TryResolve(new SkillNodeId(1), new InputToken("Fire1"), tags, out _));
            tags.Add(CommonTags.Cancel, 1, default);
            Assert.IsTrue(baked.TryResolve(new SkillNodeId(1), new InputToken("Fire1"), tags, out var next));
            Assert.AreEqual(2, next.ToSkill.Value); Assert.AreEqual(12, next.Timeline.Value);
        }
        [Test] public void ExplicitStartIsRequiredAndUsed()
        {
            table.Entries = new[] { new ComboEntryAsset { Skill = b, InputAction = "Fire1" } };
            var baked = table.Bake(new[] { a, b });
            Assert.IsTrue(baked.TryResolve(SkillNodeId.None, new InputToken("Fire1"), new TagComp(), out var next));
            Assert.AreEqual(2, next.ToSkill.Value);
        }
        [Test] public void InvalidReferencesAndDuplicateConditionsAreRejected()
        {
            table.Entries = new[] { Link() };
            Assert.Throws<InvalidOperationException>(() => table.Bake(new[] { a }));
            table.Entries = new[] { Link(), Link() };
            Assert.Throws<InvalidOperationException>(() => table.Bake());
            table.Entries = new[] { new ComboEntryAsset() };
            Assert.Throws<InvalidOperationException>(() => table.Bake());
            table.Entries = new[] { new ComboEntryAsset { Skill = b, PreSkillAssets = new BuffArenaSkillAsset[] { null } } };
            Assert.Throws<InvalidOperationException>(() => table.Bake());
        }
        [Test] public void HigherPriorityWins()
        {
            var low = Link(1); low.Skill = a;
            table.Entries = new[] { low, Link(2) };
            var tags = new TagComp(); tags.Add(CommonTags.Cancel, 1, default);
            Assert.IsTrue(table.Bake().TryResolve(new SkillNodeId(1), new InputToken("Fire1"), tags, out var next));
            Assert.AreEqual(2, next.ToSkill.Value);
        }

        [Test] public void RequiredTagsAreTheOnlyAdditionalComboCondition()
        {
            var entry = new ComboEntryAsset
            {
                PreSkillAssets = new[] { a },
                InputAction = "Fire1",
                RequiredTags = new[] { CommonTags.Invincible.Value },
                Skill = b
            };
            table.Entries = new[] { entry };
            var baked = table.Bake(new[] { a, b });
            var tags = new TagComp();
            Assert.IsFalse(baked.TryResolve(new SkillNodeId(1), new InputToken("Fire1"), tags, out _));
            tags.Add(CommonTags.Invincible, 1, default);
            Assert.IsTrue(baked.TryResolve(new SkillNodeId(1), new InputToken("Fire1"), tags, out var result));
            Assert.AreEqual(2, result.ToSkill.Value);
        }
    }
}

