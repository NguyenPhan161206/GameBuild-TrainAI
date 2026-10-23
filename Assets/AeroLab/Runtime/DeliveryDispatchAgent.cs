using System;
using AeroLab.Core;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
namespace AeroLab
{
    public sealed class DeliveryDispatchAgent : Agent
    {
        public DeliveryLab Lab;
        private int episode,lastRequest=-1;
        public static void Configure(GameObject go,DeliveryLab lab,string modelPath)
        {
            var behavior=go.AddComponent<BehaviorParameters>();behavior.BehaviorName="DeliveryDispatch";behavior.BrainParameters.VectorObservationSize=DispatchObservation.Size;
            behavior.BrainParameters.ActionSpec=ActionSpec.MakeDiscrete(9);
            if(modelPath!="")
            {
                var model=Resources.Load<ModelAsset>(modelPath);if(model==null)throw new InvalidOperationException("Dispatch model not found: "+modelPath);
                behavior.Model=model;behavior.DeterministicInference=true;behavior.BehaviorType=BehaviorType.InferenceOnly;behavior.InferenceDevice=InferenceDevice.Burst;
            }
            else if(lab.Mode=="dispatch-eval")throw new InvalidOperationException("--dispatch-model is required for evaluation");
            var agent=go.AddComponent<DeliveryDispatchAgent>();agent.Lab=lab;agent.MaxStep=0;
        }
        public override void Initialize()
        {
            if(Lab!=null && Lab.Mode=="dispatch" && !Academy.Instance.IsCommunicatorOn)
                throw new InvalidOperationException("Training mode requires a connected Python trainer; no heuristic fallback is allowed.");
        }
        public override void OnEpisodeBegin()
        {lastRequest=-1;if(Lab!=null && !Lab.InteractiveResetInProgress && episode++>0){Lab.NextScenario();}}
        private void FixedUpdate()
        {
            if(Lab.Paused || !Lab.Simulation.Running)return;
            int tick=Lab.Simulation.Tick;
            if(Lab.Simulation.AtDepot && tick%50==0 && tick!=lastRequest){lastRequest=tick;RequestDecision();}
        }
        public override void CollectObservations(VectorSensor sensor){sensor.AddObservation(Lab.Simulation.ObserveDispatch().Values);}
        public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
        {var allowed=Lab.Simulation.ObserveDispatch().Allowed;for(int i=1;i<9;i++)mask.SetActionEnabled(0,i,allowed[i]);}
        public override void OnActionReceived(ActionBuffers actions)
        {Lab.Simulation.Dispatch(new DispatchAction(actions.DiscreteActions[0]-1));}
        public void Accumulate(float reward){AddReward(reward);}
        public void FinishEpisode(){if(Lab.Simulation.Result==DeliveryResult.Horizon)EpisodeInterrupted();else EndEpisode();}
    }
}
