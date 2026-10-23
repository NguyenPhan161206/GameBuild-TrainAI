using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace AeroLab.Core
{
    public enum DeliveryPhase { Waiting, Loading, Outbound, Handover, Returning, Charging, Finished }
    public enum OrderStatus { Scheduled, Pending, Assigned, Delivered, Rejected, Failed, Unfinished }
    public enum DeliveryResult { None, Completed, Horizon, Collision, OutOfBounds, OutOfEnergy, FlightTimeout, UnsafeReturn, NoTakeoff }
    public enum DispatchRule { Fifo, EarliestDeadline }
    [Serializable] public sealed class OrderRecord
    {
        public OrderSpec Spec;
        public OrderStatus Status;
        public float DeliveredAt = -1;
        public bool OnTime;
        public string Reason = "";
    }
    public readonly struct FlightAction
    {
        public readonly Vector3 Command;
        public FlightAction(float x,float y,float z) { Command = new Vector3(x,y,z); }
        public FlightAction(Vector3 command) { Command = command; }
    }
    public readonly struct DispatchAction
    {
        // -1 means wait/charge, otherwise a stable slot in the observation.
        public readonly int Slot;
        public DispatchAction(int slot) { Slot=slot; }
    }
    public sealed class FlightObservation
    {
        public const string Version = "flight-delivery-1";
        public const int Size = 26;
        public readonly float[] Values;
        public FlightObservation(float[] values) { Values=values; }
    }
    public sealed class DispatchObservation
    {
        public const string Version = "dispatch-delivery-1";
        public const int Size = 4+8*7;
        public readonly float[] Values;
        public readonly bool[] Allowed;
        public readonly int[] OrderIds;
        public DispatchObservation(float[] values,bool[] allowed,int[] ids) { Values=values;Allowed=allowed;OrderIds=ids; }
    }
    [Serializable] public sealed class EpisodeReport
    {
        public string ReportVersion="delivery-report-2";
        public string EnvironmentVersion = ScenarioConfig.Version, FlightObservationVersion=FlightObservation.Version, DispatchObservationVersion=DispatchObservation.Version;
        public string FlightModel="reference-route", DispatchModel="fifo", SensorMode, Result;
        public string FlightModelSha256="", DispatchModelSha256="", SourceSha256="";
        public int Seed, Delivered, OnTime, Rejected, Failed, Unfinished, Trips, SafeReturns;
        public float Elapsed, Distance, Energy, MeanDeliveryLatency;
        public ScenarioConfig Config;
        public OrderRecord[] Orders;
        public LegReport[] Legs;
    }
    public sealed class DeliverySimulation
    {
        public ScenarioConfig Config { get; private set; }
        public FlightSimulation Flight { get; private set; } = new FlightSimulation();
        private readonly List<OrderRecord> orders=new List<OrderRecord>();
        public IReadOnlyList<OrderRecord> Orders {get{return orders.AsReadOnly();}}
        public DeliveryPhase Phase {get;private set;}
        public DeliveryResult Result {get;private set;}
        public OrderRecord Active {get;private set;}
        public float Battery {get;internal set;}
        public float Elapsed {get;private set;}
        public float Distance {get;private set;}
        public float Energy {get;private set;}
        public float Payload {get;private set;}
        public int Trips {get;private set;}
        public int SafeReturns {get;private set;}
        public int Tick {get;private set;}
        public bool Emergency {get;private set;}
        public bool Running {get{return Result==DeliveryResult.None;}}
        public bool AtDepot {get{return Phase==DeliveryPhase.Waiting || Phase==DeliveryPhase.Charging;}}
        private readonly Dictionary<int,float> budgets=new Dictionary<int,float>();
        private bool legAirborne;
        private int dwellTicks;
        private int cachedTick=-1;
        private FlightObservation cachedObservation;
        private readonly Queue<float[]> delayed=new Queue<float[]>();
        private SeedRandom sensorRandom;
        public Action<DeliveryTrace> TraceSink;
        private readonly List<LegReport> legs=new List<LegReport>();
        private LegReport currentLeg;
        private void Trace(string name,Vector3 command)
        {
            if(TraceSink==null)return;
            TraceSink(new DeliveryTrace{Event=name,Tick=Tick,Leg=legs.Count,Trip=Trips,OrderId=Active==null?-1:Active.Spec.Id,
                Phase=Phase.ToString(),Position=Flight.Position,Velocity=Flight.Velocity,Goal=Flight.Goal,Command=command,
                Battery=Battery,Payload=Payload,Observation=cachedObservation==null?null:(float[])cachedObservation.Values.Clone()});
        }
        private void EndLeg(string result)
        {
            if(currentLeg==null)return;
            currentLeg.Ended=Elapsed;currentLeg.BatteryEnd=Battery;currentLeg.FinalPosition=Flight.Position;
            currentLeg.FinalVelocity=Flight.Velocity;currentLeg.Result=result;currentLeg=null;
            Trace("leg_end:"+result,Vector3.Zero);
        }
        public DeliverySimulation(ScenarioConfig config) { Reset(config); }
        public void Reset(ScenarioConfig config)
        {
            config.Validate(); Config=config; orders.Clear();
            legs.Clear();currentLeg=null;
            budgets.Clear();
            foreach(var spec in config.Orders) orders.Add(new OrderRecord{Spec=spec,Status=OrderStatus.Scheduled});
            Phase=DeliveryPhase.Waiting;Result=DeliveryResult.None;Active=null;
            Battery=config.Capacity;Elapsed=Distance=Energy=Payload=0;dwellTicks=0;Tick=Trips=SafeReturns=0;Emergency=false;legAirborne=false;
            Flight.ResetLeg(config.Depot,config.Depot,config.Buildings);
            delayed.Clear();cachedTick=-1;cachedObservation=null;sensorRandom=new SeedRandom(config.Seed^73471);
            ReleaseOrders();ObserveFlight();
        }
        private void ReleaseOrders()
        {
            foreach(var o in orders.Where(o=>o.Status==OrderStatus.Scheduled && o.Spec.Release<=Elapsed).OrderBy(o=>o.Spec.Release).ThenBy(o=>o.Spec.Id))
            {
                if(orders.Count(x=>x.Status==OrderStatus.Pending)>=8) {o.Status=OrderStatus.Rejected;o.Reason="queue_full";}
                else {o.Status=OrderStatus.Pending;}
            }
        }
        public float RequiredEnergy(OrderSpec order)
        {
            float cached; if(budgets.TryGetValue(order.Id,out cached)) return cached;
            cached=ComputeEnergy(order); budgets[order.Id]=cached; return cached;
        }
        private float ComputeEnergy(OrderSpec order)
        {
            if(order.Mass>Config.MaxPayload) return float.PositiveInfinity;
            var pad=Config.Pads[order.Destination];
            float length=RoutePlanner.Length(Config.Depot,RoutePlanner.Plan(Config.Depot,pad,Config.Buildings)) + RoutePlanner.Length(pad,RoutePlanner.Plan(pad,Config.Depot,Config.Buildings));
            // Conservative reference-controller budget, not a certificate for a learned trajectory.
            return (length/2+70)*(Config.IdlePower+Config.PayloadPower*order.Mass) + length*Config.TravelCost + Config.Reserve;
        }
        public DispatchObservation ObserveDispatch()
        {
            var v=new float[DispatchObservation.Size];var allowed=new bool[9];var ids=new int[8];
            for(int i=0;i<8;i++) ids[i]=-1;
            allowed[0]=AtDepot && Running;v[0]=Battery/Config.Capacity;v[1]=Elapsed/Config.Duration;v[2]=Config.Reserve/Config.Capacity;v[3]=orders.Count(o=>o.Status==OrderStatus.Delivered)/(float)Math.Max(1,orders.Count);
            var pending=orders.Where(o=>o.Status==OrderStatus.Pending).OrderBy(o=>o.Spec.Release).ThenBy(o=>o.Spec.Id).Take(8).ToArray();
            for(int i=0;i<pending.Length;i++)
            {
                var o=pending[i];int k=4+i*7;var pad=Config.Pads[o.Spec.Destination];
                ids[i]=o.Spec.Id;v[k]=1;v[k+1]=(o.Spec.Deadline-Elapsed)/Config.Duration;v[k+2]=(Elapsed-o.Spec.Release)/Config.Duration;
                v[k+3]=o.Spec.Mass/Config.MaxPayload;v[k+4]=pad.X/40;v[k+5]=pad.Z/40;
                float need=RequiredEnergy(o.Spec); v[k+6]=float.IsInfinity(need)?2:need/Config.Capacity;
                allowed[i+1]=AtDepot && Running && need<=Battery;
            }
            return new DispatchObservation(v,allowed,ids);
        }
        public int Choose(DispatchRule rule)
        {
            var obs=ObserveDispatch();int selected=-1;float score=float.PositiveInfinity;
            for(int i=0;i<8;i++) if(obs.Allowed[i+1])
            {
                var o=orders.First(x=>x.Spec.Id==obs.OrderIds[i]);float value=rule==DispatchRule.Fifo?o.Spec.Release:o.Spec.Deadline;
                if(value<score){score=value;selected=i;}
            }
            return selected;
        }
        public bool Dispatch(DispatchAction action)
        {
            if(!Running || !AtDepot) return false;
            if(action.Slot < -1 || action.Slot >=8) throw new ArgumentOutOfRangeException(nameof(action));
            if(action.Slot==-1){ Phase=Battery<Config.Capacity?DeliveryPhase.Charging:DeliveryPhase.Waiting;return true; }
            var obs=ObserveDispatch(); if(!obs.Allowed[action.Slot+1]) return false;
            Active=orders.First(o=>o.Spec.Id==obs.OrderIds[action.Slot]);Active.Status=OrderStatus.Assigned;
            Payload=Active.Spec.Mass;Phase=DeliveryPhase.Loading;dwellTicks=50;Emergency=false;return true;
        }
        private void StartLeg(Vector3 goal,bool returning)
        {
            EndLeg("ReserveRedirect");
            legAirborne=false;var start=Flight.Position;var velocity=Flight.Velocity;
            bool airborneRedirect=returning && !Flight.Grounded;
            Flight.ResetLeg(start,goal,Config.Buildings);if(airborneRedirect)Flight.Velocity=velocity;Flight.Begin();
            Phase=returning?DeliveryPhase.Returning:DeliveryPhase.Outbound;
            delayed.Clear();cachedTick=-1;
            currentLeg=new LegReport{Leg=legs.Count+1,Trip=Trips,OrderId=Active.Spec.Id,Returning=returning,
                Start=start,Goal=goal,Started=Elapsed,BatteryStart=Battery,Payload=Payload,Result="InProgress"};
            legs.Add(currentLeg);Trace("leg_start",Vector3.Zero);
        }
        public float Step(FlightAction action)
        {
            if(!Running) return 0;
            var a=action.Command;
            if(float.IsNaN(a.X+a.Y+a.Z)||float.IsInfinity(a.X+a.Y+a.Z)) throw new ArgumentException("Non-finite action");
            if(Tick%5==0)Trace("action",a);
            float reward=-.001f*FlightSimulation.Dt;
            Tick++;Elapsed=Tick*FlightSimulation.Dt;
            ReleaseOrders();
            if(Phase==DeliveryPhase.Charging || Phase==DeliveryPhase.Waiting)
            {
                Battery=Math.Min(Config.Capacity,Battery+Config.ChargeRate*FlightSimulation.Dt);
                Phase=Battery<Config.Capacity?DeliveryPhase.Charging:DeliveryPhase.Waiting;
                // A permanently infeasible order must not keep the episode alive forever.
                foreach(var o in orders.Where(o=>o.Status==OrderStatus.Pending && RequiredEnergy(o.Spec)>Config.Capacity))
                {o.Status=OrderStatus.Rejected;o.Reason=o.Spec.Mass>Config.MaxPayload?"payload":"route_or_energy";}
            }
            else if(Phase==DeliveryPhase.Loading || Phase==DeliveryPhase.Handover)
            {
                dwellTicks--;
                if(dwellTicks<=0)
                {
                    if(Phase==DeliveryPhase.Loading){Trips++;StartLeg(Config.Pads[Active.Spec.Destination],false);}
                    else
                    {
                        Active.Status=OrderStatus.Delivered;Active.DeliveredAt=Elapsed;Active.OnTime=Elapsed<=Active.Spec.Deadline;
                        Trace("handover",Vector3.Zero);
                        reward+=Active.OnTime?10:2;Payload=0;StartLeg(Config.Depot,true);
                    }
                }
            }
            else if(Phase==DeliveryPhase.Outbound || Phase==DeliveryPhase.Returning)
            {
                var previous=Flight.Position; float flightReward=Flight.Step(a);reward+=flightReward*.1f;
                if(!legAirborne && Flight.Position.Y-FlightSimulation.Half.Y>=2){legAirborne=true;reward+=.5f;}
                float moved=Vector3.Distance(previous,Flight.Position);Distance+=moved;
                if(currentLeg!=null)currentLeg.Distance+=moved;
                float used=(Config.IdlePower+Config.PayloadPower*Payload)*FlightSimulation.Dt+moved*Config.TravelCost;
                Battery=Math.Max(0,Battery-used);Energy+=used;reward-=used*.001f;
                if(Flight.Result==FlightResult.Collision) Finish(DeliveryResult.Collision);
                else if(Flight.Result==FlightResult.OutOfBounds) Finish(DeliveryResult.OutOfBounds);
                else if(Battery<=0) Finish(DeliveryResult.OutOfEnergy);
                else if(Flight.Result==FlightResult.Timeout) Finish(DeliveryResult.FlightTimeout);
                else if(Flight.Result==FlightResult.Success)
                {
                    if(!legAirborne)Finish(DeliveryResult.NoTakeoff);
                    else
                    {
                        EndLeg("Success");
                        if(Phase==DeliveryPhase.Outbound){Phase=DeliveryPhase.Handover;dwellTicks=50;}
                        else {SafeReturns++;Payload=0;Active=null;Phase=DeliveryPhase.Charging;}
                    }
                }
                else if(Phase==DeliveryPhase.Outbound && Battery<=Config.Reserve)
                {
                    Emergency=true;Active.Status=OrderStatus.Failed;Active.Reason="reserve_return";
                    var route=RoutePlanner.Plan(Flight.Position,Config.Depot,Config.Buildings);
                    if(route==null) Finish(DeliveryResult.UnsafeReturn);else StartLeg(Config.Depot,true);
                }
            }
            if(Running && orders.All(o=>o.Status!=OrderStatus.Scheduled && o.Status!=OrderStatus.Pending && o.Status!=OrderStatus.Assigned) && AtDepot) Finish(DeliveryResult.Completed);
            if(Running && Elapsed>=Config.Duration) Finish(DeliveryResult.Horizon);
            ObserveFlight();
            if(!Running && Result!=DeliveryResult.Completed && Result!=DeliveryResult.Horizon) reward-=10;
            return reward;
        }
        private void Finish(DeliveryResult result)
        {
            EndLeg(result.ToString());Trace("episode_end:"+result,Vector3.Zero);
            Result=result;Phase=DeliveryPhase.Finished;
            foreach(var o in orders)
            {
                if(o.Status==OrderStatus.Assigned){o.Status=OrderStatus.Failed;o.Reason=result.ToString();}
                else if(o.Status==OrderStatus.Scheduled || o.Status==OrderStatus.Pending) {o.Status=OrderStatus.Unfinished;o.Reason=result.ToString();}
            }
        }
        public FlightObservation ObserveFlight()
        {
            if(cachedTick==Tick && cachedObservation!=null) return cachedObservation;
            var values=new float[FlightObservation.Size];var raw=Flight.Observe();Array.Copy(raw,values,20);
            var cfg=Config.Sensors;
            for(int i=0;i<3;i++) values[i]+=sensorRandom.Signed()*cfg.PositionNoise/80;
            values[6]=Math.Max(0,values[6]+sensorRandom.Signed()*cfg.PositionNoise/30);
            for(int i=8;i<18;i++)
                values[i]=sensorRandom.NextFloat()<cfg.DropProbability ? -1 : Math.Max(0,Math.Min(1,values[i]+sensorRandom.Signed()*cfg.RangeNoise/30));
            values[20]=Battery/Config.Capacity;values[21]=Payload/Config.MaxPayload;
            values[22]=Phase==DeliveryPhase.Returning?1:0;values[23]=Emergency?1:0;
            values[24]=Active==null?0:(Active.Spec.Deadline-Elapsed)/Config.Duration;values[25]=1;
            delayed.Enqueue(values);
            if(delayed.Count<=cfg.DelayTicks) {values=new float[FlightObservation.Size];for(int i=8;i<18;i++)values[i]=-1;}
            else values=delayed.Dequeue();
            cachedTick=Tick; cachedObservation=new FlightObservation(values);return cachedObservation;
        }
        public EpisodeReport Report(string flightModel="reference-route",string dispatchModel="fifo")
        {
            var delivered=orders.Where(o=>o.Status==OrderStatus.Delivered).ToArray();
            return new EpisodeReport{Config=Config,Seed=Config.Seed,FlightModel=flightModel,DispatchModel=dispatchModel,SensorMode=Config.Sensors.DropProbability>0 || Config.Sensors.PositionNoise>0 || Config.Sensors.RangeNoise>0 || Config.Sensors.DelayTicks>0?"noisy":"ideal",Result=Result.ToString(),Delivered=delivered.Length,OnTime=delivered.Count(o=>o.OnTime),Rejected=orders.Count(o=>o.Status==OrderStatus.Rejected),Failed=orders.Count(o=>o.Status==OrderStatus.Failed),Unfinished=orders.Count(o=>o.Status==OrderStatus.Unfinished),Trips=Trips,SafeReturns=SafeReturns,Elapsed=Elapsed,Distance=Distance,Energy=Energy,MeanDeliveryLatency=delivered.Length==0?0:delivered.Average(o=>o.DeliveredAt-o.Spec.Release),Orders=orders.ToArray(),Legs=legs.ToArray()};
        }
    }
}
