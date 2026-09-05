using System;
using System.IO;
using Combat.Config;
using Combat.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.EditorTools
{
    public static class SeasonThreeSceneBuilder
    {
        const string Root = "Assets/Scenes/HaloCombat";

        struct SceneSpec
        {
            public string FileName;
            public CombatRunner.SceneKind Kind;
            public SceneSpec(string fileName, CombatRunner.SceneKind kind) { FileName = fileName; Kind = kind; }
        }

        static readonly SceneSpec[] Specs =
        {
            new SceneSpec("V1Motor.unity", CombatRunner.SceneKind.V1),
            new SceneSpec("V2Melee.unity", CombatRunner.SceneKind.V2),
            new SceneSpec("V3ProjectileAoe.unity", CombatRunner.SceneKind.V3),
            new SceneSpec("V4AiSummon.unity", CombatRunner.SceneKind.V4)
        };

        [MenuItem("Combat/Create Season Three Scenes")]
        public static void CreateAll()
        {
            var database = AssetDatabase.LoadAssetAtPath<CombatDatabaseAsset>(
                "Assets/Combat/Config/Generated/CombatDatabase.asset");
            if (database == null)
                throw new InvalidOperationException("Generate CombatDatabase.asset before creating Season Three scenes.");
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes", "HaloCombat"));
            var existing = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < Specs.Length; i++)
            {
                string path = ScenePath(Specs[i]);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("CombatRunner");
                var runner = root.AddComponent<CombatRunner>();
                root.AddComponent<CombatGizmos>();
                runner.Configure(Specs[i].Kind, database, true);

                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = "Ground";
                plane.transform.position = Vector3.zero;
                plane.transform.localScale = Vector3.one * 2f;

                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 8f, -10f);
                camera.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
                camera.fieldOfView = 50f;

                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("Unable to save " + path);
                AddOrEnable(existing, path);
            }

            EditorBuildSettings.scenes = existing.ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HaloCombat] Created Season Three scenes: " + Specs.Length);
        }

        [MenuItem("Combat/Verify Season Three Scenes")]
        public static void VerifyAll()
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                string path = ScenePath(Specs[i]);
                if (!File.Exists(path)) throw new InvalidOperationException("Missing " + path);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var runners = UnityEngine.Object.FindObjectsByType<CombatRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (runners.Length != 1) throw new InvalidOperationException(path + " must contain exactly one CombatRunner");
                if (runners[0].Scene != Specs[i].Kind) throw new InvalidOperationException(path + " has wrong SceneKind");
                if (!runners[0].IsUsingSoDatabase || runners[0].Database == null)
                    throw new InvalidOperationException(path + " must use generated CombatDatabase.asset");
                if (!scene.isLoaded) throw new InvalidOperationException(path + " was not loaded");
            }
            Debug.Log("[HaloCombat] Verified Season Three scenes: " + Specs.Length);
        }

        static void AddOrEnable(System.Collections.Generic.List<EditorBuildSettingsScene> scenes, string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != path) continue;
                scenes[i] = new EditorBuildSettingsScene(path, true);
                return;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        static string ScenePath(SceneSpec spec) => Root + "/" + spec.FileName;
    }
}
