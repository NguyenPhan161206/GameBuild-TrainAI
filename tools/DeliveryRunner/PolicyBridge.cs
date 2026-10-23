using AeroLab.Core;
using System.Text.Json;
using System.Numerics;
// JSON-lines transport only: physics and teacher labels stay in the shared C# core.
internal static class PolicyBridge
{
    public static void Run(int count,int seed,int stage,bool noisy,string pattern="generated")
    {
        for(int episode=0;episode<count;episode++)
        {
            var cfg=ScenarioConfig.Create(seed+episode,noisy,stage);
            if(pattern!="generated")
            {
                if(pattern!="repeat" && pattern!="cycle")throw new ArgumentException("Unknown pattern");
                for(int i=0;i<cfg.Orders.Length;i++)cfg.Orders[i].Destination=pattern=="repeat"?0:i%4;
            }
            var sim=new DeliverySimulation(cfg);
            var action=Vector3.Zero;
            while(sim.Running)
            {
                if(sim.AtDepot && sim.Tick%50==0)sim.Dispatch(new DispatchAction(sim.Choose(DispatchRule.EarliestDeadline)));
                if(sim.Tick%5==0)
                {
                    var obs=sim.ObserveFlight().Values;var label=ClearancePilot.Action(obs);
                    Console.WriteLine(JsonSerializer.Serialize(new {observation=obs,teacher=new[]{label.X,label.Y,label.Z},episode,phase=sim.Phase.ToString(),
                        tick=sim.Tick,trip=sim.Trips,position=new[]{sim.Flight.Position.X,sim.Flight.Position.Y,sim.Flight.Position.Z},
                        velocity=new[]{sim.Flight.Velocity.X,sim.Flight.Velocity.Y,sim.Flight.Velocity.Z},
                        goal=new[]{sim.Flight.Goal.X,sim.Flight.Goal.Y,sim.Flight.Goal.Z}}));
                    string line=Console.ReadLine()??throw new IOException("Policy transport closed");var a=JsonSerializer.Deserialize<float[]>(line)!;
                    if(a.Length!=3)throw new InvalidDataException("Expected three actions");action=new Vector3(a[0],a[1],a[2]);
                }
                sim.Step(new FlightAction(action));
            }
            Console.WriteLine(JsonSerializer.Serialize(new {report=sim.Report("python-actor-bridge","edd")} ,new JsonSerializerOptions{IncludeFields=true}));
        }
    }
}
