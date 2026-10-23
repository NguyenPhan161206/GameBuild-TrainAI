using System;
using System.IO;
using AeroLab.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AeroLab.Editor
{
    public static class BuildTools
    {
        public const string ScenePath = "Assets/AeroLab/Scenes/FlightLab.unity";
        [MenuItem("AeroLab/Open flight arena")]
        public static void OpenArena()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(DeliveryBuildTools.ScenePath, true) };
        }

        [MenuItem("AeroLab/Play flight arena")]
        public static void PlayArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(DeliveryBuildTools.ScenePath, true) };
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("AeroLab/Validate project")]
        public static void ValidateProject()
        {
            OpenArena();
            if (UnityEngine.Object.FindFirstObjectByType<FlightLab>() == null)
                throw new InvalidOperationException("FlightLab component is missing from the scene.");
            if (Resources.Load<Material>("FlightSurface") == null)
                throw new InvalidOperationException("FlightSurface material is missing.");
            if (Resources.Load<Font>("Fonts/DejaVuSans") == null)
                throw new InvalidOperationException("Vietnamese font is missing.");
            var sim = new FlightSimulation();
            var pilot = new DemoPilot();
            sim.Begin();
            while (sim.Running) sim.Step(pilot.Action(sim));
            if (sim.Result != FlightResult.Success) throw new InvalidOperationException("Reference flight failed: " + sim.Result);
            Debug.Log("AEROLAB_PROJECT_VALIDATION_PASS");
        }

        [MenuItem("AeroLab/Run Play Mode smoke test")]
        public static void Smoke()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before starting the smoke test.");
            ValidateProject();
            var lab = UnityEngine.Object.FindAnyObjectByType<FlightLab>();
            var probe = lab.gameObject.AddComponent<RuntimeSmokeProbe>();
            probe.Delivery = false;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("AeroLab/Build/Linux x64")]
        public static void Linux() { Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AeroLab.x86_64"); }

        [MenuItem("AeroLab/Build/Windows x64")]
        public static void Windows() { Build(BuildTarget.StandaloneWindows64, "Builds/Windows/AeroLab.exe"); }

        [MenuItem("AeroLab/Build/WebGL")]
        public static void Web() { Build(BuildTarget.WebGL, "Builds/WebGL"); }

        private static void Build(BuildTarget target, string destination)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Install the build-support module for " + target + " in Unity Hub.");
            ValidateProject();
            PlayerSettings.companyName = "GameBuild-TrainAI";
            PlayerSettings.productName = "AeroLab";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.runInBackground = false;
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? "Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = destination, target = target,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Build failed: " + report.summary.result + "; errors: " + report.summary.totalErrors);
            Debug.Log("AEROLAB_BUILD_PASS: " + destination);
        }
    }
}
