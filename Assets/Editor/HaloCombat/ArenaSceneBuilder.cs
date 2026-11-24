using System;
using System.IO;
using System.Linq;
using Combat.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.EditorTools
{
    public static class ArenaSceneBuilder
    {
        public const string Path="Assets/Scenes/Arena.unity";
        [MenuItem("Combat/Create JSON Arena Scene")]
        public static void Create()
        {
            var content=UnityRuntimeContent.Load();if(content.Content.Runtime.Arena==null)throw new InvalidOperationException("Formal JSON requires runtime.arena.");
            Directory.CreateDirectory("Assets/Scenes");
            if(SceneManager.GetSceneByPath(Path).isLoaded)throw new InvalidOperationException("Close the Arena scene before rebuilding it.");
            var old=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);var root=new GameObject("Arena");root.AddComponent<ArenaBootstrap>();
                var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();
                camera.transform.position=new Vector3(0,2.5f,-2.5f);camera.transform.rotation=Quaternion.Euler(45,0,0);camera.fieldOfView=60;
                cameraObject.AddComponent<AudioListener>();var lightObject=new GameObject("Directional Light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(50,-30,0);
                if(!EditorSceneManager.SaveScene(scene,Path))throw new IOException("Unable to save Arena scene.");
                var build=EditorBuildSettings.scenes.Where(s=>s.path!=Path).ToList();build.Insert(0,new EditorBuildSettingsScene(Path,true));EditorBuildSettings.scenes=build.ToArray();
                AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.CloseScene(scene,true);if(old.IsValid()&&old.isLoaded)SceneManager.SetActiveScene(old);}
        }
    }
}
