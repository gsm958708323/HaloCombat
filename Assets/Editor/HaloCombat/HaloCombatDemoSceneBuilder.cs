using System;
using System.IO;
using Combat.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.Unity.Editor
{
    public static class HaloCombatDemoSceneBuilder
    {
        const string SceneRoot = "Assets/Scenes/HaloCombat";

        readonly struct SceneSpec
        {
            public readonly string FileName;
            public readonly HaloCombatDemoKind Demo;

            public SceneSpec(string fileName, HaloCombatDemoKind demo)
            {
                FileName = fileName;
                Demo = demo;
            }
        }

        static readonly SceneSpec[] Specs =
        {
            new SceneSpec("01TagInputDemo.unity", HaloCombatDemoKind.TagInput),
            new SceneSpec("02AttributeDemo.unity", HaloCombatDemoKind.Attribute),
            new SceneSpec("03BuffDemo.unity", HaloCombatDemoKind.Buff),
            new SceneSpec("04ActivityMotorDemo.unity", HaloCombatDemoKind.ActivityMotor),
            new SceneSpec("05ClipPayloadDemo.unity", HaloCombatDemoKind.ClipPayload),
            new SceneSpec("06MeleeDamageDemo.unity", HaloCombatDemoKind.MeleeDamage),
            new SceneSpec("07ProjectileAoeDemo.unity", HaloCombatDemoKind.ProjectileAoe),
            new SceneSpec("08SeasonOneDemo.unity", HaloCombatDemoKind.SeasonOne),
            new SceneSpec("09KnockdownDemo.unity", HaloCombatDemoKind.Knockdown),
            new SceneSpec("10DodgeHitstopDemo.unity", HaloCombatDemoKind.DodgeHitstop),
            new SceneSpec("11AuraHomingDemo.unity", HaloCombatDemoKind.AuraHoming),
            new SceneSpec("12BehaviorTreeDemo.unity", HaloCombatDemoKind.BehaviorTree),
            new SceneSpec("13PerceptionDemo.unity", HaloCombatDemoKind.Perception),
            new SceneSpec("14EnemyAiDemo.unity", HaloCombatDemoKind.EnemyAi),
            new SceneSpec("15SummonDemo.unity", HaloCombatDemoKind.Summon),
            new SceneSpec("16SeasonTwoDemo.unity", HaloCombatDemoKind.SeasonTwo)
        };

        [MenuItem("Tools/HaloCombat/Create Demo Scenes")]
        public static void CreateAll()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes", "HaloCombat"));
            var buildScenes = new EditorBuildSettingsScene[Specs.Length];

            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var host = new GameObject("HaloCombatDemoRunner");
                var runner = host.AddComponent<HaloCombatDemoRunner>();
                runner.Configure(spec.Demo);

                if (!EditorSceneManager.SaveScene(scene, ScenePath(spec)))
                    throw new InvalidOperationException($"Unable to save {ScenePath(spec)}");
                buildScenes[i] = new EditorBuildSettingsScene(ScenePath(spec), true);
            }

            EditorBuildSettings.scenes = buildScenes;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HaloCombat] Created {Specs.Length} demo scenes.");
        }

        [MenuItem("Tools/HaloCombat/Verify Demo Scenes")]
        public static void VerifyAll()
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                var scene = EditorSceneManager.OpenScene(ScenePath(spec), OpenSceneMode.Single);
                var runners = UnityEngine.Object.FindObjectsByType<HaloCombatDemoRunner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                if (runners.Length != 1)
                    throw new InvalidOperationException($"{ScenePath(spec)} must contain exactly one HaloCombatDemoRunner.");
                if (runners[0].Demo != spec.Demo)
                    throw new InvalidOperationException($"{ScenePath(spec)} selects {runners[0].Demo}, expected {spec.Demo}.");

                runners[0].RunDemo();
                if (!scene.isLoaded)
                    throw new InvalidOperationException($"{ScenePath(spec)} was not loaded.");
            }

            Debug.Log($"[HaloCombat] Verified {Specs.Length} demo scenes.");
        }

        static string ScenePath(in SceneSpec spec)
        {
            return $"{SceneRoot}/{spec.FileName}";
        }
    }
}
