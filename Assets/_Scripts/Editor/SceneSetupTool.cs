using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StellarHaven.EditorTools
{
    /// <summary>
    /// 一键创建最小可运行场景并写入 Build Settings。
    /// </summary>
    public static class SceneSetupTool
    {
        private const string BootstrapScenePath = "Assets/_Scenes/Bootstrap.unity";
        private const string MainScenePath = "Assets/_Scenes/Main.unity";

        [MenuItem("StellarHaven/Setup/Create Bootstrap & Main Scenes")]
        public static void CreateMinimumScenes()
        {
            EnsureScene(BootstrapScenePath, addBootstrapController: true);
            EnsureScene(MainScenePath, addBootstrapController: false);
            EnsureBuildSettings();

            Debug.Log("✅ 已创建最小场景并更新 Build Settings。");
        }

        private static void EnsureScene(string scenePath, bool addBootstrapController)
        {
            if (System.IO.File.Exists(scenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            if (addBootstrapController)
            {
                GameObject bootstrapGo = new GameObject("BootstrapController");
                bootstrapGo.AddComponent<StellarHaven.Scenes.BootstrapController>();
            }

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void EnsureBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            AddIfMissing(scenes, BootstrapScenePath);
            AddIfMissing(scenes, MainScenePath);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddIfMissing(List<EditorBuildSettingsScene> scenes, string path)
        {
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == path)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
    }
}
