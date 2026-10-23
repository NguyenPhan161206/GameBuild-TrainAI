using System;
using AeroLab.Core;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Demonstrations;
using Unity.InferenceEngine;
using NVector3=System.Numerics.Vector3;
namespace AeroLab
{
    public sealed class DeliveryFlightAgent : Agent
    {
        public DeliveryLab Lab;
        private int episode;
        private bool flightTraining;
        public static void Configure(GameObject go,DeliveryLab lab,string modelPath)
        {
            var behavior=go.AddComponent<BehaviorParameters>();behavior.BehaviorName="DeliveryFlight";
            behavior.BrainParameters.VectorObservationSize=FlightObservation.Size;
            behavior.BrainParameters.ActionSpec=ActionSpec.MakeContinuous(3);
            bool train=lab.Mode=="flight"; bool demo=lab.Mode=="flight-demo";
            if(modelPath!="")
            {
                var model=Resources.Load<ModelAsset>(modelPath);if(model==null)throw new InvalidOperationException("Flight model not found in Resources: "+modelPath);
                behavior.Model=model;behavior.DeterministicInference=true;behavior.BehaviorType=BehaviorType.InferenceOnly;behavior.InferenceDevice=InferenceDevice.Burst;
            }
            else if(!train && !demo)throw new InvalidOperationException("A frozen --flight-model is required for evaluation/dispatch");
            else behavior.BehaviorType=demo?BehaviorType.HeuristicOnly:BehaviorType.Default;
            var agent=go.AddComponent<DeliveryFlightAgent>();agent.Lab=lab;agent.MaxStep=0;agent.flightTraining=train||demo||lab.Mode=="flight-eval";
            var requester=go.AddComponent<DecisionRequester>();requester.DecisionPeriod=5;requester.TakeActionsBetweenDecisions=true;
            if(demo){var recorder=go.AddComponent<DemonstrationRecorder>();recorder.DemonstrationName="delivery-flight";recorder.DemonstrationDirectory="artifacts/demonstrations";recorder.Record=true;}
        }
        public override void Initialize()
        {
            if(Lab!=null && Lab.Mode=="flight" && !Academy.Instance.IsCommunicatorOn)
                throw new InvalidOperationException("Training mode requires a connected Python trainer; no heuristic fallback is allowed.");
        }
        public override void OnEpisodeBegin()
        {
            if(Lab==null)return;
            if(Lab.InteractiveResetInProgress)return;
            if(flightTraining && episode++>0){Lab.NextScenario();}
        }
        public override void CollectObservations(VectorSensor sensor)
        {sensor.AddObservation(Lab.Simulation.ObserveFlight().Values);}
        public override void Heuristic(in ActionBuffers actionsOut)
        {var a=Lab.ReferenceAction();var values=actionsOut.ContinuousActions;values[0]=a.X;values[1]=a.Y;values[2]=a.Z;}
        public override void OnActionReceived(ActionBuffers actions)
        {
            if(Lab.Paused || !Lab.Simulation.Running)return;
            var s=Lab.Simulation;
            if(Lab.DispatchAgent==null && s.AtDepot && s.Tick%50==0)s.Dispatch(new DispatchAction(s.Choose(Lab.Rule)));
            var a=actions.ContinuousActions;
            float reward=Lab.Advance(new NVector3(a[0],a[1],a[2]));
            if(flightTraining)AddReward(reward);
            if(Lab.DispatchAgent!=null)Lab.DispatchAgent.Accumulate(reward);
            if(!s.Running)
            {
                if(flightTraining){if(s.Result==DeliveryResult.Horizon)EpisodeInterrupted();else EndEpisode();}
                else Lab.DispatchAgent.FinishEpisode();
            }
        }
    }
}
