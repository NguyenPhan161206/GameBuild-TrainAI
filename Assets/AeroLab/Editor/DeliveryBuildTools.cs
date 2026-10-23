using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AeroLab.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AeroLab.Editor
{
    public static class DeliveryBuildTools
    {
        public const string ScenePath="Assets/AeroLab/Scenes/DeliveryLab.unity";
        [MenuItem("AeroLab/Open delivery campus")]
        public static void Open()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(BuildTools.ScenePath,true),new EditorBuildSettingsScene(ScenePath,true)};
        }
        [MenuItem("AeroLab/Validate delivery project")]
        public static void Validate()
        {
            Open();if(UnityEngine.Object.FindFirstObjectByType<DeliveryLab>()==null)throw new InvalidOperationException("Missing DeliveryLab");
            var s=new DeliverySimulation(ScenarioConfig.Create(42,false,0));var pilot=new RoutePilot();var goal=new System.Numerics.Vector3(float.NaN);
            while(s.Running)
            {
                if(s.AtDepot && s.Tick%50==0)s.Dispatch(new DispatchAction(s.Choose(DispatchRule.Fifo)));
                if(goal!=s.Flight.Goal){pilot.Reset(s.Flight);goal=s.Flight.Goal;}
                s.Step(new FlightAction(pilot.Action(s.Flight)));
            }
            if(s.Result!=DeliveryResult.Completed || s.SafeReturns!=1)throw new InvalidOperationException("Delivery core smoke failed: "+s.Result);
            Debug.Log("AEROLAB_DELIVERY_VALIDATION_PASS");
        }
        [MenuItem("AeroLab/Run delivery Play Mode smoke")]
        public static void Smoke()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before running smoke checks");
            Validate();
            var lab=UnityEngine.Object.FindAnyObjectByType<DeliveryLab>();
            var probe=lab.gameObject.AddComponent<RuntimeSmokeProbe>();probe.Delivery=true;
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("AeroLab/Run layout editor Play Mode smoke")]
        public static void LayoutSmoke()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before running smoke checks");
            Validate();
            var lab=UnityEngine.Object.FindAnyObjectByType<DeliveryLab>();
            lab.Mode="dispatch-eval";lab.FlightModel="Models/campus-flight-s11";lab.DispatchModel="Models/campus-dispatch-s11";
            lab.Stage=3;lab.EvaluationCount=0;lab.AllowBatchInteractionForTests=true;
            lab.LayoutStoragePath="artifacts/layout-checks/"+Guid.NewGuid().ToString("N")+".json";
            lab.gameObject.AddComponent<RuntimeLayoutSmokeProbe>();
            EditorApplication.EnterPlaymode();
        }
        [MenuItem("AeroLab/Build/Delivery Linux x64")]
        public static void Linux()
        {
            Validate();Directory.CreateDirectory("Builds/Delivery");PlayerSettings.runInBackground=true;PlayerSettings.companyName="GameBuild-TrainAI";PlayerSettings.productName="AeroLab Delivery";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            var sources=Directory.GetFiles("Assets/AeroLab","*",SearchOption.AllDirectories).Where(p=>p.EndsWith(".cs")||p.EndsWith(".asmdef")||p.EndsWith(".unity")||p.EndsWith(".provenance.json")).Concat(new[]{"Packages/manifest.json","ProjectSettings/ProjectVersion.txt","tools/prepare-mlagents.py"}).OrderBy(p=>p,StringComparer.Ordinal);
            using(var sha=SHA256.Create())
            using(var bytes=new MemoryStream())
            {
                foreach(var source in sources){var name=Encoding.UTF8.GetBytes(source.Replace('\\','/'));bytes.Write(name,0,name.Length);var content=File.ReadAllBytes(source);bytes.Write(content,0,content.Length);}
                string digest=BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-","").ToLowerInvariant();
                File.WriteAllText("Assets/AeroLab/Resources/DeliveryBuildInfo.json","{\"sourceSha256\":\""+digest+"\"}");
            }
            AssetDatabase.Refresh();
            string version=Guid.NewGuid().ToString("N");
            string destination="Builds/DeliveryVersions/"+version+"/AeroLabDelivery.x86_64";
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath,BuildTools.ScenePath},locationPathName=destination,target=BuildTarget.StandaloneLinux64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Delivery build failed");
            if(!File.Exists(Path.GetDirectoryName(destination)+"/AeroLabDelivery_Data/Plugins/x86_64/libgrpc_csharp_ext.x64.so"))
                throw new InvalidOperationException("Linux trainer native library is missing from x86_64 plugins.");
            const string launcher="Builds/Delivery/AeroLabDelivery.x86_64";
            string temporary=launcher+".tmp";
            File.WriteAllText(temporary,"#!/bin/sh\nexec \"$(dirname \"$0\")/../DeliveryVersions/"+version+"/AeroLabDelivery.x86_64\" \"$@\"\n");
            using(var chmod=System.Diagnostics.Process.Start("/bin/chmod","+x "+temporary))
            {chmod.WaitForExit();if(chmod.ExitCode!=0)throw new InvalidOperationException("Cannot mark launcher executable");}
            if(File.Exists(launcher))File.Replace(temporary,launcher,null);else File.Move(temporary,launcher);
            File.WriteAllText("Builds/Delivery/current.json","{\"version\":\""+version+"\"}");
            Debug.Log("AEROLAB_DELIVERY_BUILD_PASS: "+destination);
        }
    }
}
