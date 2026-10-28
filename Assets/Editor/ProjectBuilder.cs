using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GroundhogAI.Editor
{
    public static class ProjectBuilder
    {
        public static string Configure()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 360; camera.backgroundColor = new Color(0.08f, 0.10f, 0.12f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(640, -360, -10);
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            var game = new GameObject("Groundhog AI");
            game.AddComponent<GameHost>(); game.AddComponent<GameRenderer>();
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GroundhogAI.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/GroundhogAI.unity", true) };
            PlayerSettings.companyName = "Local Research"; PlayerSettings.productName = "GroundhogAI";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 840;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AssetDatabase.SaveAssets();
            return "GroundhogAI scene and Windows build settings saved.";
        }

        public static void BuildWindows()
        {
            Configure();
            string output = Path.GetFullPath("Build/Windows/GroundhogAI.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GroundhogAI.unity" },
                target = BuildTarget.StandaloneWindows64, locationPathName = output, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("GROUNDHOG_BUILD_SUCCEEDED " + output);
        }
    }
}
