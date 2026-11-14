using System;
using System.Collections.Generic;
using System.IO;
using Combat.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.EditorTools
{
    public static class SkillLabSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/HaloCombat/SkillLab.unity";

        [MenuItem("Combat/Create JSON Skill Lab Scene")]
        public static void Create()
        {
            Directory.CreateDirectory("Assets/Scenes/HaloCombat");
            // Build additively so an unsaved working scene is never discarded by the tool.
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var host = new GameObject("SkillLab");
                host.AddComponent<SkillLabRunner>(); host.AddComponent<SkillLabVisuals>();
                var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 10;
                camera.transform.position = new Vector3(1, 20, 0); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .1f, .12f);
                var lightObject = new GameObject("Directional Light");
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unable to save " + ScenePath);
                var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                int index = build.FindIndex(entry => entry.path == ScenePath);
                if (index < 0) build.Add(new EditorBuildSettingsScene(ScenePath, true));
                else build[index] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = build.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
