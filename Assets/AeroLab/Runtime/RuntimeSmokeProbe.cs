#if UNITY_EDITOR
using System;
using AeroLab.Core;
using UnityEditor;
using UnityEngine;
namespace AeroLab
{
    // Exists only in the Editor. Public fields survive Play Mode assembly reloads.
    public sealed class RuntimeSmokeProbe : MonoBehaviour
    {
        public bool Delivery;
        public float Deadline;
        public bool Started;
        private bool finished;
        public string RuntimeFailure;
        private void OnEnable(){Application.logMessageReceived+=OnLog;}
        private void OnDisable(){Application.logMessageReceived-=OnLog;}
        private void OnLog(string message,string stack,LogType type)
        {
            if((type==LogType.Exception || type==LogType.Error) && stack.Contains("AeroLab"))RuntimeFailure=message;
        }
        private void Update()
        {
            if(finished || !Application.isPlaying)return;
            if(!string.IsNullOrEmpty(RuntimeFailure)){Finish(false,RuntimeFailure);return;}
            Application.runInBackground=true;
            Time.timeScale=20;
            if(Deadline==0)Deadline=Time.realtimeSinceStartup+240;
            if(Time.realtimeSinceStartup>Deadline){Finish(false,"Runtime smoke exceeded 240 seconds");return;}
            try
            {
                if(Delivery)
                {
                    var lab=GetComponent<DeliveryLab>();
                    if(lab==null || lab.Simulation==null)return;
                    lab.Resume();
                    if(lab.EpisodeCount==0)return;
                    var report=lab.LastReport;
                    if(report==null || report.Delivered==0 || report.SafeReturns==0 || (report.Result!="Completed" && report.Result!="Horizon"))
                        throw new InvalidOperationException("Delivery smoke did not complete a safe sortie");
                    lab.ResetEpisode();
                    if(lab.Simulation.Elapsed!=0 || lab.Simulation.Energy!=0 || lab.Simulation.Report().Delivered!=0)
                        throw new InvalidOperationException("Delivery reset retained episode state");
                    Finish(true,"AEROLAB_DELIVERY_PLAYMODE_PASS");
                }
                else
                {
                    var lab=GetComponent<FlightLab>();
                    if(lab==null || lab.Simulation==null)return;
                    if(!Started || (!lab.Simulation.Running && lab.Simulation.Result==FlightResult.None))
                    {lab.StartDemo();Started=true;}
                    if(lab.Paused)lab.TogglePause();
                    if(lab.Simulation.Result==FlightResult.None)return;
                    if(lab.Simulation.Result!=FlightResult.Success)throw new InvalidOperationException("Reference flight failed: "+lab.Simulation.Result);
                    lab.ResetFlight();
                    if(lab.Simulation.Running || lab.Simulation.Elapsed!=0 || lab.Simulation.TotalReward!=0)throw new InvalidOperationException("Flight reset failed");
                    lab.StartManual();lab.TogglePause();
                    if(!lab.Paused || !lab.Simulation.Running)throw new InvalidOperationException("Flight pause failed");
                    Finish(true,"AEROLAB_PLAYMODE_SMOKE_PASS");
                }
            }
            catch(Exception error){Finish(false,error.ToString());}
        }
        private void Finish(bool pass,string message)
        {
            finished=true;
            if(pass)Debug.Log(message);else Debug.LogError(message);
            if(Application.isBatchMode)EditorApplication.Exit(pass?0:1);else EditorApplication.ExitPlaymode();
        }
    }
}
#endif
