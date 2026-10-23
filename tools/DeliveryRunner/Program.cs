using AeroLab.Core;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;
if(args.Length>0 && args[0]=="--control-samples")
{ControlSamples.Generate(args[1],args.Length>2?int.Parse(args[2]):100000,811);return;}
if(args.Length>0 && args[0]=="--policy-bridge")
{PolicyBridge.Run(int.Parse(args[1]),int.Parse(args[2]),int.Parse(args[3]),args.Length>4 && bool.Parse(args[4]),args.Length>5?args[5]:"generated");return;}
int count=args.Length>0?int.Parse(args[0]):100;
int offset=args.Length>1?int.Parse(args[1]):20000;
string output=args.Length>2?args[2]:"artifacts/delivery-baselines.json";
if(count<1)throw new ArgumentOutOfRangeException(nameof(count));
var reports=new List<EpisodeReport>();
foreach(bool noisy in new[]{false,true}) foreach(var rule in new[]{DispatchRule.Fifo,DispatchRule.EarliestDeadline})
for(int i=0;i<count;i++)
{
    var sim=new DeliverySimulation(ScenarioConfig.Create(offset+i,noisy));var pilot=new RoutePilot();Vector3 lastGoal=new Vector3(float.NaN);
    while(sim.Running)
    {
        if(sim.AtDepot && sim.Tick%50==0) sim.Dispatch(new DispatchAction(sim.Choose(rule)));
        if(sim.Flight.Goal!=lastGoal) {pilot.Reset(sim.Flight);lastGoal=sim.Flight.Goal;}
        sim.Step(new FlightAction(pilot.Action(sim.Flight)));
    }
    reports.Add(sim.Report("reference-route",rule.ToString()));
}
var options=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};options.Converters.Add(new JsonStringEnumConverter());
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output,JsonSerializer.Serialize(reports,options));
foreach(var group in reports.GroupBy(x=>x.SensorMode+"/"+x.DispatchModel))
Console.WriteLine($"{group.Key}: episodes={group.Count()} delivered={group.Sum(x=>x.Delivered)} on_time={group.Sum(x=>x.OnTime)} safe_returns={group.Sum(x=>x.SafeReturns)} incidents={group.Count(x=>x.Result!="Completed" && x.Result!="Horizon")} energy={group.Average(x=>x.Energy):F2}");
Console.WriteLine(output);
