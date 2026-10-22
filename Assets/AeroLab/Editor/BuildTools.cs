using System;
using System.IO;
using AeroLab.Core;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AeroLab.Editor
{
    [InitializeOnLoad]
    public static class BuildTools
    {
        public const string ScenePath = "Assets/AeroLab/Scenes/FlightLab.unity";
        private const string SmokeKey = "AeroLab.Smoke.Active";
        private const string StartedKey = "AeroLab.Smoke.Started";
        private const string DeadlineKey = "AeroLab.Smoke.Deadline";

        static BuildTools() { EditorApplication.update += UpdateSmoke; }

        [MenuItem("AeroLab/Open flight arena")]
        public static void OpenArena()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        [MenuItem("AeroLab/Play flight arena")]
        public static void PlayArena()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
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
            SessionState.SetBool(SmokeKey, true);
            SessionState.SetBool(StartedKey, false);
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + 120);
            EditorApplication.EnterPlaymode();
        }

        private static void UpdateSmoke()
        {
            if (!SessionState.GetBool(SmokeKey, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0))
            { FinishSmoke(false, "Play Mode smoke test exceeded 120 seconds."); return; }
            if (!EditorApplication.isPlaying) return;
            try
            {
                FlightLab lab = UnityEngine.Object.FindFirstObjectByType<FlightLab>();
                if (lab == null || lab.Simulation == null) return;
                if (!SessionState.GetBool(StartedKey, false))
                {
                    lab.StartDemo();
                    SessionState.SetBool(StartedKey, true);
                    return;
                }
                if (lab.Simulation.Result == FlightResult.None) return;
                if (lab.Simulation.Result != FlightResult.Success) throw new InvalidOperationException("Demo ended: " + lab.Simulation.Result);
                lab.ResetFlight();
                if (lab.Simulation.Running || lab.Simulation.Elapsed != 0 || lab.Simulation.TotalReward != 0)
                    throw new InvalidOperationException("Reset did not clear runtime state.");
                lab.StartManual();
                lab.TogglePause();
                if (!lab.Paused || !lab.Simulation.Running) throw new InvalidOperationException("Pause is broken.");
                FinishSmoke(true, "AEROLAB_PLAYMODE_SMOKE_PASS");
            }
            catch (Exception error) { FinishSmoke(false, error.ToString()); }
        }

        private static void FinishSmoke(bool passed, string message)
        {
            SessionState.SetBool(SmokeKey, false);
            SessionState.SetBool(StartedKey, false);
            if (passed) Debug.Log(message); else Debug.LogError(message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.ExitPlaymode();
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
