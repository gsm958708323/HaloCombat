using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Game
{
    public static class BuffArenaRenderUtility
    {
        static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>(64);

        public static void Normalize(GameObject root)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var source = renderer.sharedMaterials;
                if (source == null || source.Length == 0) continue;
                var result = new Material[source.Length];
                bool particle = renderer is ParticleSystemRenderer;
                for (int j = 0; j < source.Length; j++)
                    result[j] = NormalizeMaterial(source[j], particle);
                renderer.sharedMaterials = result;
            }
        }

        static Material NormalizeMaterial(Material source, bool particle)
        {
            if (source == null) return null;
            string shaderName = source.shader != null ? source.shader.name : string.Empty;
            if (shaderName.StartsWith("Universal Render Pipeline/")) return source;
            int key = source.GetInstanceID() * 2 + (particle ? 1 : 0);
            if (Materials.TryGetValue(key, out var cached)) return cached;

            Shader shader = particle
                ? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                : Shader.Find("Universal Render Pipeline/Lit");
            shader = shader ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return source;

            var material = new Material(shader) { name = source.name + "_BuffArenaURP" };
            Texture texture = source.HasProperty("_MainTex")
                ? source.GetTexture("_MainTex")
                : source.mainTexture;
            if (texture != null)
            {
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                if (source.HasProperty("_MainTex") && material.HasProperty("_BaseMap"))
                {
                    material.SetTextureScale("_BaseMap", source.GetTextureScale("_MainTex"));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset("_MainTex"));
                }
            }
            Color color = source.HasProperty("_Color")
                ? source.GetColor("_Color")
                : source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
            if (particle && source.HasProperty("_TintColor"))
                color = source.GetColor("_TintColor");
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (source.HasProperty("_Cutoff") && material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            if (particle)
                ConfigureParticleMaterial(material);
            Materials[key] = material;
            return material;
        }

        static void ConfigureParticleMaterial(Material material)
        {
            if (material == null) return;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
