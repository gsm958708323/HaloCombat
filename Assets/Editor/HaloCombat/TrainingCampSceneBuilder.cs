using System;
using System.Collections.Generic;
using System.IO;
using Combat.TrainingCamp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.EditorTools
{
    /// <summary>Dedicated builder. It intentionally does not touch any old demo scene.</summary>
    public static class TrainingCampSceneBuilder
    {
        const string Path = "Assets/Scenes/HaloCombat/TrainingCamp.unity";
        [MenuItem("Combat/Create Training Camp Sandbox")]
        public static void Create()
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Application.dataPath, "Scenes", "HaloCombat"));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var host = new GameObject("TrainingCamp");
        host.AddComponent<TrainingCampRunner>(); host.AddComponent<TrainingCampController>(); host.AddComponent<TrainingCampPanel>(); host.AddComponent<TrainingCampVisuals>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Validation Ground"; ground.transform.localScale = Vector3.one * 5f;
            AssignGroundMaterial(ground);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera"; var camera = cameraObject.AddComponent<Camera>(); camera.transform.position = new Vector3(0f, 10f, -13f); camera.transform.rotation = Quaternion.Euler(38f, 0f, 0f); camera.fieldOfView = 52f;
            var lightObject = new GameObject("Directional Light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            if (!EditorSceneManager.SaveScene(scene, Path)) throw new InvalidOperationException("Unable to save " + Path);
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); AddOrEnable(build); EditorBuildSettings.scenes = build.ToArray(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[HaloCombat] Created TrainingCamp.unity");
        }
        [MenuItem("Combat/Verify Training Camp Sandbox")]
        public static void Verify()
        {
            if (!File.Exists(Path)) throw new InvalidOperationException("Missing " + Path);
            var scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Single);
            var runners = UnityEngine.Object.FindObjectsByType<TrainingCampRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (runners.Length != 1 || !scene.isLoaded) throw new InvalidOperationException("TrainingCamp must contain one loaded TrainingCampRunner");
            runners[0].ResetWorld();
            if (!TrainingCampProbe.Check(runners[0].World, runners[0].PlayerId, runners[0].DummyId)) throw new InvalidOperationException("TrainingCamp initial probe failed");
            Debug.Log("[HaloCombat] Verified TrainingCamp sandbox");
        }
        static void AssignGroundMaterial(GameObject ground)
        {
            var renderer = ground != null ? ground.GetComponent<Renderer>() : null;
            if (renderer == null) return;
            const string materialPath = "Assets/Settings/TrainingCampGround.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                if (shader == null) throw new InvalidOperationException("Unable to find a supported TrainingCamp ground shader");
                material = new Material(shader) { name = "TrainingCampGround" };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.025f, 0.06f, 0.12f, 1f));
                material.color = new Color(0.025f, 0.06f, 0.12f, 1f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            renderer.sharedMaterial = material;
        }
        static void AddOrEnable(List<EditorBuildSettingsScene> scenes)
        {
            for (int i = 0; i < scenes.Count; i++) if (scenes[i].path == Path) { scenes[i] = new EditorBuildSettingsScene(Path, true); return; }
            scenes.Add(new EditorBuildSettingsScene(Path, true));
        }
    }
}
