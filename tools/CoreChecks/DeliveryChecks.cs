using System;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using AeroLab.Core;
internal static class DeliveryChecks
{
    public static void Run(Action<string,Action> check)
    {
        check("Trace is observational and leg reports close once per transition",()=>{
            var c=ScenarioConfig.Create(23,true,0);var a=new DeliverySimulation(c);var b=new DeliverySimulation(c);
            int events=0;b.TraceSink=frame=>{events++;Assert(frame.Observation==null || frame.Observation.Length==26);};
            var pilot=new RoutePilot();var goal=new Vector3(float.NaN);
            while(a.Running)
            {
                if(a.AtDepot && a.Tick%50==0){int slot=a.Choose(DispatchRule.Fifo);a.Dispatch(new DispatchAction(slot));b.Dispatch(new DispatchAction(slot));}
                if(a.Flight.Goal!=goal){pilot.Reset(a.Flight);goal=a.Flight.Goal;}
                var action=new FlightAction(pilot.Action(a.Flight));a.Step(action);b.Step(action);
                Assert(a.ObserveFlight().Values.SequenceEqual(b.ObserveFlight().Values));
            }
            Assert(events>0 && DeliveryReportJson.Serialize(a.Report())==DeliveryReportJson.Serialize(b.Report()));
            var report=b.Report();Assert(report.Legs.Length==2 && report.Legs.All(x=>x.Result=="Success"));
            Assert(report.Legs[0].OrderId==report.Legs[1].OrderId && report.Legs[1].Returning);
            Assert(Math.Abs(report.Legs.Sum(x=>x.Distance)-report.Distance)<.01f);
            b.Reset(c);Assert(b.Report().Legs.Length==0);
        });
        check("Many-sortie reference report retains horizon separately from completed legs",()=>{
            var s=Run(ScenarioConfig.Create(42,false,3));var r=s.Report();
            Assert(r.Result=="Horizon" || r.Result=="Completed");Assert(r.SafeReturns>1 && r.Legs.Length>2);
            Assert(r.Legs.Count(x=>x.Returning && x.Result=="Success")==r.SafeReturns);
            Assert(r.Legs.All(x=>x.Result!="InProgress") && r.Legs.Select(x=>x.Leg).Distinct().Count()==r.Legs.Length);
        });
        check("Delivery roundtrip, no duplicate handover, completion freezes and reset clears",()=>{
            var c=ScenarioConfig.Create(1,false,0);var s=Run(c);
            Assert(s.Result==DeliveryResult.Completed,$"{s.Result} at {s.Flight.Position}");Assert(s.Report().Delivered==1 && s.SafeReturns==1);
            int tick=s.Tick; s.Step(new FlightAction(Vector3.One));Assert(tick==s.Tick);
            s.Reset(c);Assert(s.Report().Delivered==0 && s.Trips==0 && s.Battery==c.Capacity && s.Tick==0);
        });
        check("Delivery layout variation, A* and routes across independent seeds",()=>{
            var a=ScenarioConfig.Create(2);var b=ScenarioConfig.Create(3);Assert(a.Buildings[0].Half!=b.Buildings[0].Half);
            for(int i=0;i<20;i++)
            {
                var c=ScenarioConfig.Create(i,false,1);var s=Run(c);
                Assert(s.Result==DeliveryResult.Completed && s.SafeReturns==1,$"seed={i} {s.Result} {s.Flight.Position}");
            }
        });
        check("Vector-only clearance teacher completes held-out ideal roundtrips",()=>{
            for(int seed=10000;seed<10020;seed++)
            {
                var sim=new DeliverySimulation(ScenarioConfig.Create(seed,false,1));
                while(sim.Running)
                {
                    if(sim.AtDepot && sim.Tick%50==0)sim.Dispatch(new DispatchAction(sim.Choose(DispatchRule.Fifo)));
                    sim.Step(new FlightAction(ClearancePilot.Action(sim.ObserveFlight().Values)));
                }
                Assert(sim.Result==DeliveryResult.Completed && sim.SafeReturns==1,$"clearance seed {seed}: {sim.Result}");
            }
        });
        check("Payload, energy gate and invalid dispatch do not mutate mission",()=>{
            var c=ScenarioConfig.Create(0,false,0);c.Orders[0].Mass=3;var s=new DeliverySimulation(c);
            Assert(!s.Dispatch(new DispatchAction(0)) && s.Active==null);s.Step(new FlightAction(Vector3.Zero));Assert(s.Orders[0].Status==OrderStatus.Rejected);
            c=ScenarioConfig.Create(0,false,0);s.Reset(c);s.Battery=1;Assert(!s.Dispatch(new DispatchAction(0)));s.Dispatch(new DispatchAction(-1));float battery=s.Battery;s.Step(new FlightAction(Vector3.Zero));Assert(s.Battery>battery);
            bool invalid=false;try{s.Dispatch(new DispatchAction(8));}catch(ArgumentOutOfRangeException){invalid=true;}Assert(invalid);
        });
        check("Eight-slot queue, future releases, deadlines and episode horizon",()=>{
            var c=ScenarioConfig.Create(0);foreach(var o in c.Orders)o.Release=0;
            var s=new DeliverySimulation(c);Assert(s.Orders.Count(o=>o.Status==OrderStatus.Pending)==8 && s.Report().Rejected==4);
            c=ScenarioConfig.Create(0,false,0);c.Orders[0].Deadline=1;s=Run(c);Assert(s.Report().Delivered==1 && s.Report().OnTime==0);
            c=ScenarioConfig.Create(0);c.Duration=1;s=new DeliverySimulation(c);for(int i=0;i<50;i++)s.Step(new FlightAction(Vector3.Zero));Assert(s.Result==DeliveryResult.Horizon);
            Assert(s.Report().Unfinished+s.Report().Rejected==c.Orders.Length);
        });
        check("Noisy samples are deterministic, delayed, missing and cached",()=>{
            var c=ScenarioConfig.Create(4,true,0);c.Sensors.DropProbability=1;c.Sensors.DelayTicks=2;
            var a=new DeliverySimulation(c);var b=new DeliverySimulation(c);
            for(int i=0;i<10;i++)
            {
                a.Step(new FlightAction(Vector3.Zero));b.Step(new FlightAction(Vector3.Zero));
                var x=a.ObserveFlight().Values;var y=b.ObserveFlight().Values;
                Assert(x.Length==26 && x.SequenceEqual(y) && x.SequenceEqual(a.ObserveFlight().Values));
                Assert(x.Skip(8).Take(10).All(v=>v==-1));
            }
            Assert(a.ObserveDispatch().Values.Length==60);
        });
        check("Reserve abort, collision, invalid flight action and energy exhaustion",()=>{
            var c=ScenarioConfig.Create(0,false,0);var s=new DeliverySimulation(c);s.Dispatch(new DispatchAction(0));
            while(s.Phase==DeliveryPhase.Loading)s.Step(new FlightAction(Vector3.Zero));
            s.Flight.Position+=Vector3.UnitY*3;s.Flight.Velocity=Vector3.UnitX;
            s.Battery=c.Reserve; s.Step(new FlightAction(Vector3.UnitY));Assert(s.Flight.Velocity.X>.9f);Assert(s.Emergency && s.Phase==DeliveryPhase.Returning && s.Active.Status==OrderStatus.Failed);
            s.Battery=.00001f;s.Step(new FlightAction(Vector3.Zero));Assert(s.Result==DeliveryResult.OutOfEnergy);
            s.Reset(c);int tick=s.Tick;bool invalid=false;try{s.Step(new FlightAction(float.NaN,0,0));}catch(ArgumentException){invalid=true;}Assert(invalid && s.Tick==tick);
            s.Dispatch(new DispatchAction(0));while(s.Phase==DeliveryPhase.Loading)s.Step(new FlightAction(Vector3.Zero));
            s.Flight.Position=new Vector3(0,.51f,0);s.Flight.Velocity=-Vector3.UnitY*2;s.Step(new FlightAction(Vector3.Zero));Assert(s.Result==DeliveryResult.Collision);
        });
        check("Loading uses exactly 50 ticks and reports preserve full geometry and escaping",()=>{
            var s=new DeliverySimulation(ScenarioConfig.Create(1,false,1));s.Dispatch(new DispatchAction(0));
            for(int i=0;i<49;i++)s.Step(new FlightAction(Vector3.Zero));Assert(s.Phase==DeliveryPhase.Loading);
            s.Step(new FlightAction(Vector3.Zero));Assert(s.Phase==DeliveryPhase.Outbound && Math.Abs(s.Elapsed-1)<.00001f);
            string name="model \"trial\"";
            using(var report=JsonDocument.Parse(DeliveryReportJson.Serialize(s.Report(name))))
            {
                Assert(report.RootElement.GetProperty("FlightModel").GetString()==name);
                Assert(report.RootElement.GetProperty("Config").GetProperty("Buildings").GetArrayLength()==6);
                Assert(report.RootElement.GetProperty("Config").GetProperty("Buildings")[0].GetProperty("Center").GetProperty("X").ValueKind==JsonValueKind.Number);
            }
        });
        check("Ground-only arrival is not a completed flight",()=>{
            var c=ScenarioConfig.Create(0,false,0);var s=new DeliverySimulation(c);s.Dispatch(new DispatchAction(0));
            while(s.Phase==DeliveryPhase.Loading)s.Step(new FlightAction(Vector3.Zero));
            s.Flight.Position=s.Flight.Goal;s.Flight.Velocity=Vector3.Zero;
            for(int i=0;i<50;i++)s.Step(new FlightAction(Vector3.Zero));
            Assert(s.Result==DeliveryResult.NoTakeoff && s.Report().Delivered==0);
        });
        check("Blocked route is rejected and configuration validation catches bad sensors",()=>{
            var boxes=new[]{new Building(new Vector3(0,15,0),new Vector3(2,15,40))};
            Assert(RoutePlanner.Plan(new Vector3(-20,.5f,0),new Vector3(20,.5f,0),boxes)==null);
            var c=ScenarioConfig.Create();c.Sensors.DropProbability=float.NaN;bool invalid=false;try{c.Validate();}catch(ArgumentException){invalid=true;}Assert(invalid);
        });
    }
    private static DeliverySimulation Run(ScenarioConfig c)
    {
        var s=new DeliverySimulation(c);var pilot=new RoutePilot();var goal=new Vector3(float.NaN);
        while(s.Running)
        {
            if(s.AtDepot && s.Tick%50==0)s.Dispatch(new DispatchAction(s.Choose(DispatchRule.Fifo)));
            if(s.Flight.Goal!=goal){pilot.Reset(s.Flight);goal=s.Flight.Goal;}
            s.Step(new FlightAction(pilot.Action(s.Flight)));
        }
        return s;
    }
    private static void Assert(bool value,string detail="") {if(!value)throw new Exception("Delivery assertion: "+detail);}
}
