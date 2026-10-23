#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using AeroLab.Core;
using UnityEditor;
using UnityEngine;
namespace AeroLab
{
    // Tests the real frozen flight/dispatch agents, including policy callbacks on manual reset.
    public sealed class RuntimeLayoutSmokeProbe : MonoBehaviour
    {
        public int Stage;
        public float Deadline;
        public string Failure;
        private bool finished;
        private void OnEnable(){Application.logMessageReceived+=OnLog;}
        private void OnDisable(){Application.logMessageReceived-=OnLog;}
        private void OnLog(string message,string stack,LogType type)
        {if((type==LogType.Error || type==LogType.Exception) && stack.Contains("AeroLab"))Failure=message;}
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private void Fresh(DeliveryLab lab,int seed)
        {
            var sim=lab.Simulation;
            Check(lab.Paused && lab.Seed==seed && sim.Tick==0 && sim.Energy==0 && sim.Distance==0,"Reset retained motion or advanced seed");
            Check(sim.Battery==sim.Config.Capacity && sim.Payload==0 && sim.Active==null && sim.Trips==0 && sim.SafeReturns==0,"Reset retained mission state");
            Check(sim.Flight.Position==sim.Config.Depot && sim.Flight.Velocity==System.Numerics.Vector3.Zero && lab.LastReport==null,"Reset retained flight/report state");
            Check(sim.Orders.All(o=>o.Status==OrderStatus.Pending || o.Status==OrderStatus.Scheduled),"Reset retained order state");
            var actions=lab.FlightAgent.GetStoredActionBuffers().ContinuousActions;
            for(int i=0;i<actions.Length;i++)Check(actions[i]==0,"Reset retained flight action");
        }
        private void Update()
        {
            if(finished || !Application.isPlaying)return;
            if(Deadline==0)Deadline=Time.realtimeSinceStartup+180;
            try
            {
                Check(string.IsNullOrEmpty(Failure),Failure);
                Check(Time.realtimeSinceStartup<Deadline,"Layout smoke exceeded 180 seconds");
                var lab=GetComponent<DeliveryLab>();if(lab==null || lab.Simulation==null || lab.DispatchAgent==null || lab.FlightAgent==null)return;
                Time.timeScale=100;Application.runInBackground=true;
                if(Stage==0){lab.Resume();Stage=1;return;}
                if(Stage==1)
                {
                    if(lab.Simulation.Phase!=DeliveryPhase.Outbound || lab.Simulation.Flight.Elapsed<2)return;
                    int seed=lab.Seed;var orders=lab.Simulation.Config.Orders;
                    Check(lab.RestartInteractive(),"Reset refused in inference mode");Fresh(lab,seed);
                    Check(lab.ApplyBuilding(0,-18,-12,28),"Valid edit refused: "+lab.LayoutMessage+"; seed="+lab.Seed+" count="+lab.Simulation.Config.Buildings.Length);Fresh(lab,seed);
                    Check(lab.Simulation.Flight.Buildings[0].Half.Y==14,"Edited geometry not used by core");
                    var simulation=lab.Simulation;
                    Check(!lab.ApplyBuilding(0,-30,-30,20) && ReferenceEquals(lab.Simulation,simulation),"Invalid edit mutated running scenario");
                    Check(lab.SaveInteractiveLayout() && File.Exists(lab.LayoutFilePath),"Layout save failed");
                    Check(lab.ShuffleInteractive(),"Shuffle failed");Fresh(lab,seed);
                    Check(lab.Simulation.Config.LayoutSeed!=seed,"Layout seed did not change");
                    for(int i=0;i<orders.Length;i++)Check(lab.Simulation.Config.Orders[i].Release==orders[i].Release && lab.Simulation.Config.Orders[i].Destination==orders[i].Destination,"Shuffle changed order stream");
                    Check(lab.LoadInteractiveLayout() && lab.Simulation.Config.Buildings[0].Half.Y==14,"Layout reload failed");Fresh(lab,seed);
                    File.WriteAllText(lab.LayoutFilePath,"{\"Version\":99}");simulation=lab.Simulation;
                    Check(!lab.LoadInteractiveLayout() && ReferenceEquals(simulation,lab.Simulation),"Invalid file changed simulation");
                    Check(lab.RestoreGeneratedLayout(),"Restore failed");Fresh(lab,seed);
                    Check(lab.Simulation.Config.LayoutOrigin=="generated","Restore retained custom origin");
                    lab.EvaluationCount=1;Check(!lab.ApplyBuilding(0,-18,-12,28) && !lab.RestartInteractive() && !lab.ShuffleInteractive() && !lab.LoadInteractiveLayout(),"Official evaluation accepts UI edits");lab.EvaluationCount=0;
                    string mode=lab.Mode;lab.Mode="dispatch";Check(!lab.CanEditScenario && !lab.RestartInteractive(),"Training accepts UI reset");lab.Mode=mode;
                    lab.Resume();Stage=2;return;
                }
                if(Stage==2)
                {
                    if(lab.Simulation.Phase!=DeliveryPhase.Outbound || lab.Simulation.Flight.Elapsed<2)return;
                    // Shorten this test episode only to exercise terminal callback -> manual reset.
                    lab.Simulation.Config.Duration=lab.Simulation.Elapsed+.5f;Stage=3;return;
                }
                if(Stage==3)
                {
                    if(lab.Simulation.Running)return;
                    Check(lab.Paused && lab.LastReport!=null && lab.LastReport.RunPurpose=="interactive","Terminal inference did not pause/report");
                    int seed=lab.Seed;Check(lab.RestartInteractive(),"Terminal reset failed");Fresh(lab,seed);
                    Check(lab.RestoreGeneratedLayout() && lab.Simulation.Config.Duration==900,"Original duration not restored");
                    Finish(true,"AEROLAB_LAYOUT_PLAYMODE_PASS");
                }
            }
            catch(Exception error){Finish(false,error.ToString());}
        }
        private void Finish(bool pass,string message)
        {
            finished=true;if(pass)Debug.Log(message);else Debug.LogError(message);
            if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);else EditorApplication.ExitPlaymode();
        }
    }
}
#endif
