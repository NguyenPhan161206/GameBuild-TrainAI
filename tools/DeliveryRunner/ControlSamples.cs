using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using AeroLab.Core;
internal static class ControlSamples
{
    public static void Generate(string path,int count,int seed)
    {
        var rng=new SeedRandom(seed);var cfg=ScenarioConfig.Create(seed,false,0);
        var goals=new[]{cfg.Depot,cfg.Pads[0],cfg.Pads[1],cfg.Pads[2],cfg.Pads[3]};
        var flight=new FlightSimulation();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("AEROLAB1"));writer.Write(count);writer.Write(26);writer.Write(3);
            for(int i=0;i<count;i++)
            {
                int goalIndex=(int)(rng.NextFloat()*5)%5;var goal=goals[goalIndex];
                flight.ResetLeg(cfg.Depot,goal,Array.Empty<Building>());
                float x=rng.Signed()*35,z=rng.Signed()*35,y=.5f+rng.NextFloat()*27.5f;
                float vx=rng.Signed()*4,vz=rng.Signed()*4,vy=rng.Signed()*2;
                switch(i%5)
                {
                    case 0:x=goal.X+rng.Signed()*1.6f;z=goal.Z+rng.Signed()*1.6f;y=rng.NextFloat()<.6f?.5f:.5f+rng.NextFloat()*2.5f;vx=rng.Signed()*.8f;vz=rng.Signed()*.8f;vy=rng.Signed()*.6f;break;
                    case 1:
                        float radius=1+rng.NextFloat()*11,angle=rng.NextFloat()*6.2831853f;
                        x=goal.X+(float)Math.Cos(angle)*radius;z=goal.Z+(float)Math.Sin(angle)*radius;
                        y=.5f+Math.Max(0,Math.Min(27.5f,-.2f+25.7f*Math.Max(0,Math.Min(1,(radius-1)/8))+rng.Signed()*4));break;
                    case 2:y=24+rng.NextFloat()*4;vy=rng.Signed();break;
                    case 3:var start=goals[(goalIndex+1)%5];x=start.X+rng.Signed();z=start.Z+rng.Signed();y=.5f+rng.NextFloat()*7;vx=rng.Signed()*2;vz=rng.Signed()*2;vy=rng.NextFloat()*2;break;
                }
                x=Math.Max(-37,Math.Min(37,x));z=Math.Max(-37,Math.Min(37,z));
                if(y==.5f)vy=0;
                flight.Position=new Vector3(x,y,z);flight.Velocity=new Vector3(vx,vy,vz);
                var obs=new float[26];Array.Copy(flight.Observe(),obs,20);
                // Nuisance augmentation teaches the height-prior controller not to depend
                // on correlated empty-arena ranges. These are not physical rollouts.
                for(int j=8;j<18;j++)obs[j]=rng.NextFloat()<.01f?-1:rng.NextFloat();
                obs[18]=rng.NextFloat();obs[19]=flight.OnTarget?rng.NextFloat():0;
                obs[20]=.2f+rng.NextFloat()*.8f;obs[21]=rng.NextFloat();obs[22]=goalIndex==0?1:0;
                obs[23]=rng.NextFloat()<.02f?1:0;obs[24]=rng.NextFloat()*1.5f-.5f;obs[25]=1;
                var action=ClearancePilot.Action(obs);
                foreach(float value in obs)writer.Write(value);
                writer.Write(action.X);writer.Write(action.Y);writer.Write(action.Z);
            }
        }
        File.WriteAllText(path+".json",JsonSerializer.Serialize(new {schema=FlightObservation.Version,count,seed,teacher="scripted vector-only clearance controller",cruise_prior_m=26,building_height_bound_m=24,synthetic_states=true,physical_rollouts=false,range_nuisance_augmentation=true}));
        Console.WriteLine(path);
    }
}
