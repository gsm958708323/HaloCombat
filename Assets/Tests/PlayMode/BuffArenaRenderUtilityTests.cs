using Combat.Unity.Game;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class BuffArenaRenderUtilityTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NormalizationRebuildsDestroyedCachedMaterials(bool particle)
        {
            var sourceShader = Shader.Find("Standard");
            Assert.IsNotNull(sourceShader);
            var source = new Material(sourceShader);
            var tint = new Color(.25f, .5f, .75f, .8f);
            source.color = tint;
            source.mainTexture = Texture2D.whiteTexture;
            var first = new GameObject("FirstMaterialUse");
            var next = new GameObject("MaterialUseAfterCleanup");
            Material converted = null;
            Material rebuilt = null;
            try
            {
                var firstRenderer = AddRenderer(first, particle);
                var nextRenderer = AddRenderer(next, particle);
                firstRenderer.sharedMaterial = source;
                nextRenderer.sharedMaterial = source;
                BuffArenaRenderUtility.Normalize(first);
                converted = firstRenderer.sharedMaterial;
                Assert.IsNotNull(converted);
                BuffArenaRenderUtility.Normalize(next);
                Assert.AreSame(converted, nextRenderer.sharedMaterial, "Live materials should remain shared.");

                // Simulate Unity cleaning native objects while static managed fields survive.
                Object.DestroyImmediate(converted);
                Assert.IsTrue(converted == null);
                nextRenderer.sharedMaterial = source;
                BuffArenaRenderUtility.Normalize(next);
                rebuilt = nextRenderer.sharedMaterial;
                Assert.IsTrue(rebuilt != null, "A destroyed cached material must be rebuilt.");
                Assert.AreEqual(particle ? "Universal Render Pipeline/Particles/Unlit"
                    : "Universal Render Pipeline/Lit", rebuilt.shader.name);
                Assert.That(Vector4.Distance(tint, rebuilt.GetColor("_BaseColor")), Is.LessThan(.0001f));
                Assert.AreSame(Texture2D.whiteTexture, rebuilt.GetTexture("_BaseMap"));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(next);
                if (converted != null) Object.DestroyImmediate(converted);
                if (rebuilt != null) Object.DestroyImmediate(rebuilt);
                Object.DestroyImmediate(source);
            }
        }

        static Renderer AddRenderer(GameObject target, bool particle)
        {
            if (!particle) return target.AddComponent<MeshRenderer>();
            target.AddComponent<ParticleSystem>();
            return target.GetComponent<ParticleSystemRenderer>();
        }
    }
}
