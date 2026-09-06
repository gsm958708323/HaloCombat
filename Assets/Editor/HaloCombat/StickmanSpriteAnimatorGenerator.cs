using System;
using System.Collections.Generic;
using System.IO;
using Combat.Core;
using Combat.Unity.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HaloCombat.Editor
{
    /// <summary>
    /// Builds the sprite Animator controllers and prefabs used by the combat presentation layer.
    /// </summary>
    public static class StickmanSpriteAnimatorGenerator
    {
        const string SourceRoot = "Assets/Art/Stickman/Source/OpenGameArt_CC0/Stick Figure Character Sprites 2D";
        const string EnemyRoot = "Assets/Art/Stickman/Generated/Enemies";
        const string ViewRoot = "Assets/Art/Stickman/Generated/Views";
        const string ResourcesRoot = "Assets/Resources/Stickman";
        const string ViewTablePath = ResourcesRoot + "/StickmanViews.asset";

        static readonly BlueprintSpec[] Blueprints =
        {
            new BlueprintSpec("fighter", "Neutral", "Fighter sprites", "fighter"),
            new BlueprintSpec("swordsman", "Neutral", "Sword sprites", "sword"),
            new BlueprintSpec("gunslinger", "Neutral", "Pistol sprites", "pistol"),
            new BlueprintSpec("melee_ai", "Crimson", "Fighter sprites", "fighter"),
            new BlueprintSpec("melee_guard", "Gold", "Sword sprites", "sword"),
            new BlueprintSpec("ranged_ai", "Toxic", "Pistol sprites", "pistol")
        };

        static readonly HashSet<string> ConfiguredImporters = new HashSet<string>(StringComparer.Ordinal);

        readonly struct BlueprintSpec
        {
            public readonly string Id;
            public readonly string Palette;
            public readonly string Folder;
            public readonly string FilePrefix;

            public BlueprintSpec(string id, string palette, string folder, string filePrefix)
            {
                Id = id;
                Palette = palette;
                Folder = folder;
                FilePrefix = filePrefix;
            }
        }

        sealed class SpriteSet
        {
            public List<Sprite> Idle;
            public List<Sprite> Run;
            public List<Sprite> Attack;
            public List<Sprite> Air;
            public List<Sprite> Dash;
            public List<Sprite> Slide;
            public List<Sprite> Hit;
            public List<Sprite> Death;
        }

        sealed class ClipSet
        {
            public AnimationClip Idle;
            public AnimationClip Run;
            public AnimationClip Attack;
            public AnimationClip Air;
            public AnimationClip Dash;
            public AnimationClip Slide;
            public AnimationClip Hit;
            public AnimationClip Death;
        }

        [MenuItem("HaloCombat/Stickman/Generate Sprite Animator Views")]
        public static void GenerateViews()
        {
            ConfiguredImporters.Clear();
            EnsureDirectory(ViewRoot);
            EnsureDirectory(ResourcesRoot);
            for (int i = 0; i < Blueprints.Length; i++)
                EnsureDirectory($"{ViewRoot}/{Blueprints[i].Id}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var entries = new List<ViewPrefabEntry>(Blueprints.Length);
            for (int i = 0; i < Blueprints.Length; i++)
            {
                var spec = Blueprints[i];
                var sprites = LoadSpriteSet(spec);
                if (sprites == null || sprites.Idle.Count == 0)
                {
                    Debug.LogError($"Cannot build Sprite Animator for '{spec.Id}'. Check the source and palette assets.");
                    continue;
                }

                var clips = CreateClipSet(spec, sprites);
                var controller = CreateController(spec, clips);
                var prefab = CreatePrefab(spec, sprites.Idle[0], controller);
                if (prefab != null)
                {
                    entries.Add(new ViewPrefabEntry
                    {
                        BlueprintId = spec.Id,
                        Prefab = prefab
                    });
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            WriteViewTable(entries);
            AssetDatabase.SaveAssets();

            Debug.Log($"Generated {entries.Count} stickman Sprite Animator views. " +
                      "Assign the generated StickmanViews asset to ArenaBootstrap.Views.");
            EditorUtility.DisplayDialog(
                "Stickman Sprite Animator",
                $"Generated {entries.Count} Sprite Animator views.\n" +
                "Run the Arena scene to preview them.",
                "OK");
        }

        static SpriteSet LoadSpriteSet(BlueprintSpec spec)
        {
            var folder = spec.Palette == "Neutral"
                ? $"{SourceRoot}/{spec.Folder}"
                : $"{EnemyRoot}/{spec.Palette}/{spec.Folder}";
            var absoluteFolder = ProjectFilePath(folder);
            if (!Directory.Exists(absoluteFolder))
            {
                if (spec.Palette != "Neutral")
                    Debug.LogError($"Missing palette folder '{folder}'. Run Generate Enemy Palette Sprites first.");
                return null;
            }

            var files = Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var set = new SpriteSet
            {
                Idle = LoadAction(files, spec.FilePrefix, "idle"),
                Run = LoadAction(files, spec.FilePrefix, "run"),
                Attack = LoadAction(files, spec.FilePrefix, spec.FilePrefix == "pistol" ? "shot" : "combo"),
                Air = LoadAction(files, spec.FilePrefix, "air_attack"),
                Dash = LoadAction(files, spec.FilePrefix, "dash"),
                Slide = LoadAction(files, spec.FilePrefix, "slide"),
                Hit = LoadAction(files, spec.FilePrefix, "hit"),
                Death = LoadAction(files, spec.FilePrefix, "death")
            };

            set.Run = UseFallback(set.Run, set.Idle);
            set.Attack = UseFallback(set.Attack, set.Idle);
            set.Air = UseFallback(set.Air, set.Idle);
            set.Dash = UseFallback(set.Dash, set.Run);
            set.Slide = UseFallback(set.Slide, set.Dash);
            set.Hit = UseFallback(set.Hit, set.Idle);
            set.Death = UseFallback(set.Death, set.Hit);
            return set;
        }

        static List<Sprite> LoadAction(string[] files, string prefix, string action)
        {
            var result = new List<Sprite>(16);
            var token = "_" + action + "_";
            for (int i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileNameWithoutExtension(files[i]);
                var lower = name.ToLowerInvariant();
                if (!lower.StartsWith(prefix + "_", StringComparison.Ordinal) || !lower.Contains(token))
                    continue;

                var path = AssetPath(files[i]);
                ConfigureSpriteImporter(path);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    result.Add(sprite);
                else
                    Debug.LogWarning($"Could not load sprite '{path}'.");
            }

            return result;
        }

        static List<Sprite> UseFallback(List<Sprite> sprites, List<Sprite> fallback)
        {
            return sprites != null && sprites.Count > 0 ? sprites : new List<Sprite>(fallback);
        }

        static ClipSet CreateClipSet(BlueprintSpec spec, SpriteSet sprites)
        {
            var folder = $"{ViewRoot}/{spec.Id}";
            EnsureDirectory(folder);
            return new ClipSet
            {
                Idle = CreateClip($"{folder}/Idle.anim", "Idle", sprites.Idle, true),
                Run = CreateClip($"{folder}/Run.anim", "Run", sprites.Run, true),
                Attack = CreateClip($"{folder}/Attack.anim", "Attack", sprites.Attack, false),
                Air = CreateClip($"{folder}/Air.anim", "Air", sprites.Air, true),
                Dash = CreateClip($"{folder}/Dash.anim", "Dash", sprites.Dash, false),
                Slide = CreateClip($"{folder}/Slide.anim", "Slide", sprites.Slide, false),
                Hit = CreateClip($"{folder}/Hit.anim", "Hit", sprites.Hit, false),
                Death = CreateClip($"{folder}/Death.anim", "Death", sprites.Death, false)
            };
        }

        static AnimationClip CreateClip(string path, string name, List<Sprite> sprites, bool loop)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var clip = new AnimationClip
            {
                name = name,
                frameRate = 12f,
                wrapMode = loop ? WrapMode.Loop : WrapMode.Once
            };
            var binding = EditorCurveBinding.PPtrCurve("Sprite", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = sprites[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static AnimatorController CreateController(BlueprintSpec spec, ClipSet clips)
        {
            var path = $"{ViewRoot}/{spec.Id}/{spec.Id}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AddParameter(controller, "Grounded", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "InAir", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Attack", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Hit", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Downed", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "IFrame", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Hitstop", AnimatorControllerParameterType.Bool);
            AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AddParameter(controller, "SkillId", AnimatorControllerParameterType.Int);
            AddParameter(controller, "SkillMode", AnimatorControllerParameterType.Int);

            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle");
            var run = machine.AddState("Run");
            var attack = machine.AddState("Attack");
            var airAttack = machine.AddState("AirAttack");
            var dash = machine.AddState("Dash");
            var slide = machine.AddState("Slide");
            var air = machine.AddState("Air");
            var hit = machine.AddState("Hit");
            var downed = machine.AddState("Downed");
            var death = machine.AddState("Death");
            idle.motion = clips.Idle;
            run.motion = clips.Run;
            attack.motion = clips.Attack;
            airAttack.motion = clips.Air;
            dash.motion = clips.Dash;
            slide.motion = clips.Slide;
            air.motion = clips.Air;
            hit.motion = clips.Hit;
            downed.motion = clips.Hit;
            death.motion = clips.Death;
            machine.defaultState = idle;

            AddAnyCondition(machine, death, "Dead", AnimatorConditionMode.If);
            AddAnyCondition(machine, downed, "Downed", AnimatorConditionMode.If);
            AddAnyCondition(machine, hit, "Hit", AnimatorConditionMode.If);
            AddAnyCondition(machine, airAttack, "SkillMode", AnimatorConditionMode.Equals, (int)SkillAnimationMode.AirAttack);
            AddAnyCondition(machine, dash, "SkillMode", AnimatorConditionMode.Equals, (int)SkillAnimationMode.Dash);
            AddAnyCondition(machine, slide, "SkillMode", AnimatorConditionMode.Equals, (int)SkillAnimationMode.Slide);
            AddAnyCondition(machine, attack, "Attack", AnimatorConditionMode.If);
            AddAnyCondition(machine, air, "InAir", AnimatorConditionMode.If);

            AddCondition(idle.AddTransition(run), "Speed", AnimatorConditionMode.Greater, 0.1f);
            AddCondition(run.AddTransition(idle), "Speed", AnimatorConditionMode.Less, 0.1f);
            AddCondition(run.AddTransition(idle), "Grounded", AnimatorConditionMode.IfNot, 0f);

            AddExitToIdle(attack, idle);
            AddExitToIdle(airAttack, idle);
            AddExitToIdle(dash, idle);
            AddExitToIdle(slide, idle);
            AddExitToIdle(hit, idle);
            AddExitToIdle(downed, idle);
            AddCondition(air.AddTransition(idle), "InAir", AnimatorConditionMode.IfNot, 0f);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            controller.AddParameter(name, type);
        }

        static AnimatorStateTransition AddAnyCondition(
            AnimatorStateMachine machine,
            AnimatorState target,
            string parameter,
            AnimatorConditionMode mode,
            float threshold = 0f)
        {
            var transition = machine.AddAnyStateTransition(target);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0f;
            transition.AddCondition(mode, threshold, parameter);
            return transition;
        }

        static void AddCondition(
            AnimatorStateTransition transition,
            string parameter,
            AnimatorConditionMode mode,
            float threshold)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
            transition.AddCondition(mode, threshold, parameter);
        }

        static void AddExitToIdle(AnimatorState state, AnimatorState idle)
        {
            var transition = state.AddTransition(idle);
            transition.hasExitTime = true;
            transition.exitTime = 0.95f;
            transition.hasFixedDuration = true;
            transition.duration = 0.05f;
        }

        static GameObject CreatePrefab(BlueprintSpec spec, Sprite idleSprite, AnimatorController controller)
        {
            var path = $"{ViewRoot}/{spec.Id}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var root = new GameObject("StickmanView_" + spec.Id);
            root.AddComponent<StickmanSpriteFacing>();
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(root.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = idleSprite;
            renderer.sortingOrder = 10;

            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void WriteViewTable(List<ViewPrefabEntry> entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<ViewPrefabTable>(ViewTablePath);
            if (table == null)
            {
                table = ScriptableObject.CreateInstance<ViewPrefabTable>();
                AssetDatabase.CreateAsset(table, ViewTablePath);
            }

            table.Entries = entries.ToArray();
            EditorUtility.SetDirty(table);
        }

        static void ConfigureSpriteImporter(string path)
        {
            if (ConfiguredImporters.Contains(path))
                return;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.26f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            ConfiguredImporters.Add(path);
        }

        static void EnsureDirectory(string assetPath)
        {
            Directory.CreateDirectory(ProjectFilePath(assetPath));
        }

        static string ProjectFilePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
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
