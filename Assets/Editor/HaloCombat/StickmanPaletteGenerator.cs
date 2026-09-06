using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HaloCombat.Editor
{
    /// <summary>
    /// Keeps the CC0 source sprites untouched and creates recoloured enemy sets from them.
    /// </summary>
    public static class StickmanPaletteGenerator
    {
        const string SourceRoot = "Assets/Art/Stickman/Source/OpenGameArt_CC0/Stick Figure Character Sprites 2D";
        const string GeneratedRoot = "Assets/Art/Stickman/Generated/Enemies";

        static readonly string[] SourceFolders =
        {
            "Fighter sprites",
            "Sword sprites",
            "Pistol sprites"
        };

        readonly struct Palette
        {
            public readonly string Id;
            public readonly Color BaseColor;

            public Palette(string id, Color baseColor)
            {
                Id = id;
                BaseColor = baseColor;
            }
        }

        static readonly Palette[] EnemyPalettes =
        {
            new Palette("Crimson", new Color(0.90f, 0.16f, 0.12f)),
            new Palette("Toxic", new Color(0.20f, 0.78f, 0.28f)),
            new Palette("Void", new Color(0.52f, 0.18f, 0.88f)),
            new Palette("Gold", new Color(0.95f, 0.62f, 0.10f))
        };

        [MenuItem("HaloCombat/Stickman/Configure CC0 Sprite Import")]
        public static void ConfigureSourceSprites()
        {
            var sourcePaths = FindSourcePaths();
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                var importer = AssetImporter.GetAtPath(sourcePaths[i]) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                SetCustomSpritePivot(importer);
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"Configured {sourcePaths.Count} stickman source sprites.");
        }

        [MenuItem("HaloCombat/Stickman/Generate Enemy Palette Sprites")]
        public static void GenerateEnemyPalettes()
        {
            var sourcePaths = FindSourcePaths();
            if (sourcePaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Stickman palette generation",
                    "No source PNGs were found. Import the CC0 pack into Assets/Art/Stickman/Source first.",
                    "OK");
                return;
            }

            var previousReadable = PrepareReadableSources(sourcePaths);
            var generated = 0;
            var skipped = 0;

            try
            {
                for (int i = 0; i < sourcePaths.Count; i++)
                {
                    var sourcePath = sourcePaths[i];
                    var source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                    if (source == null)
                    {
                        Debug.LogWarning($"Could not load source sprite: {sourcePath}");
                        continue;
                    }

                    var sourcePixels = source.GetPixels32();
                    for (int p = 0; p < EnemyPalettes.Length; p++)
                    {
                        var palette = EnemyPalettes[p];
                        var outputPath = OutputPath(sourcePath, palette.Id);
                        var outputFile = ProjectFilePath(outputPath);
                        if (File.Exists(outputFile))
                        {
                            skipped++;
                            continue;
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                        var tinted = Tint(sourcePixels, palette.BaseColor);
                        var output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                        try
                        {
                            output.SetPixels32(tinted);
                            // EncodeToPNG needs the CPU-side texture data to remain readable.
                            output.Apply(false, false);
                            var png = output.EncodeToPNG();
                            File.WriteAllBytes(outputFile, png);
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(output);
                        }
                        generated++;
                    }
                }
            }
            finally
            {
                RestoreReadableSources(previousReadable);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureGeneratedSprites();
            AssetDatabase.SaveAssets();

            Debug.Log($"Generated {generated} enemy sprites; skipped {skipped} existing files.");
            EditorUtility.DisplayDialog(
                "Stickman palette generation",
                $"Generated {generated} enemy sprites in {GeneratedRoot}.\nSkipped {skipped} existing files.",
                "OK");
        }

        static List<string> FindSourcePaths()
        {
            var paths = new List<string>(256);
            for (int i = 0; i < SourceFolders.Length; i++)
            {
                var folder = $"{SourceRoot}/{SourceFolders[i]}";
                var absolute = ProjectFilePath(folder);
                if (!Directory.Exists(absolute))
                    continue;

                var files = Directory.GetFiles(absolute, "*.png", SearchOption.AllDirectories);
                for (int f = 0; f < files.Length; f++)
                    paths.Add(AssetPath(files[f]));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        static Dictionary<string, bool> PrepareReadableSources(List<string> sourcePaths)
        {
            var previous = new Dictionary<string, bool>(sourcePaths.Count, StringComparer.Ordinal);
            for (int i = 0; i < sourcePaths.Count; i++)
            {
                var path = sourcePaths[i];
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;

                previous[path] = importer.isReadable;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                SetCustomSpritePivot(importer);
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return previous;
        }

        static void RestoreReadableSources(Dictionary<string, bool> previous)
        {
            foreach (var item in previous)
            {
                var importer = AssetImporter.GetAtPath(item.Key) as TextureImporter;
                if (importer == null || importer.isReadable == item.Value)
                    continue;

                importer.isReadable = item.Value;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureGeneratedSprites()
        {
            var absolute = ProjectFilePath(GeneratedRoot);
            if (!Directory.Exists(absolute))
                return;

            var files = Directory.GetFiles(absolute, "*.png", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                var importer = AssetImporter.GetAtPath(AssetPath(files[i])) as TextureImporter;
                if (importer == null)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 100f;
                SetCustomSpritePivot(importer);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        static Color32[] Tint(Color32[] source, Color baseColor)
        {
            var result = new Color32[source.Length];
            var dark = Color.Lerp(Color.black, baseColor, 0.18f);
            var light = Color.Lerp(baseColor, Color.white, 0.20f);

            for (int i = 0; i < source.Length; i++)
            {
                var pixel = source[i];
                if (pixel.a == 0)
                {
                    result[i] = pixel;
                    continue;
                }

                var luminance = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
                var color = Color.Lerp(dark, light, Mathf.Clamp01(luminance));
                result[i] = new Color32(
                    (byte)Mathf.RoundToInt(color.r * 255f),
                    (byte)Mathf.RoundToInt(color.g * 255f),
                    (byte)Mathf.RoundToInt(color.b * 255f),
                    pixel.a);
            }

            return result;
        }

        static void SetCustomSpritePivot(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.26f);
            importer.SetTextureSettings(settings);
        }

        static string OutputPath(string sourcePath, string paletteId)
        {
            var relative = sourcePath.Substring((SourceRoot + "/").Length);
            return $"{GeneratedRoot}/{paletteId}/{relative}";
        }

        static string ProjectFilePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var relative = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relative);
        }

        static string AssetPath(string absolutePath)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(absolutePath);
            return "Assets" + fullPath.Substring(assetsRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
