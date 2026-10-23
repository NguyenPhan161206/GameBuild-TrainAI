using System;
using System.Collections.Generic;
using System.Numerics;
namespace AeroLab.Core
{
    // Four-neighbour A* at 16m. Hull-expanded swept tests also validate ascent/descent.
    public static class RoutePlanner
    {
        public const float Cruise = 16;
        public static bool Clear(Vector3 a, Vector3 b, IReadOnlyList<Building> boxes)
        {
            var delta = b-a; float length = delta.Length();
            foreach (var box in boxes)
            {
                var half = box.Half + FlightSimulation.Half + new Vector3(1f);
                if (FlightSimulation.RayBox(a, length > 0 ? delta/length : Vector3.Zero, box.Center-half, box.Center+half, length) >= 0) return false;
            }
            return true;
        }
        private static Vector3 Point(int id) { return new Vector3(-36 + id%37*2, Cruise, -36 + id/37*2); }
        private static int Cell(Vector3 p) { return (int)Math.Round((p.Z+36)/2)*37 + (int)Math.Round((p.X+36)/2); }
        public static List<Vector3> Plan(Vector3 start, Vector3 goal, IReadOnlyList<Building> boxes)
        {
            var up = new Vector3(start.X,Cruise,start.Z); var down = new Vector3(goal.X,Cruise,goal.Z);
            if (!Clear(start,up,boxes) || !Clear(down,goal,boxes)) return null;
            var raw = new List<Vector3> { up };
            if (!Clear(up,down,boxes))
            {
                int first=Cell(up), last=Cell(down), count=37*37;
                if (!Clear(up,Point(first),boxes) || !Clear(Point(last),down,boxes)) return null;
                var cost = new float[count]; var parent = new int[count]; var closed = new bool[count];
                for(int i=0;i<count;i++) { cost[i]=float.PositiveInfinity; parent[i]=-1; }
                cost[first]=0; var open = new List<int>{first};
                while(open.Count>0)
                {
                    int best=0;
                    for(int i=1;i<open.Count;i++) if(cost[open[i]]+Vector3.Distance(Point(open[i]),Point(last)) < cost[open[best]]+Vector3.Distance(Point(open[best]),Point(last))) best=i;
                    int current=open[best]; open.RemoveAt(best);
                    if(current==last) break;
                    if(closed[current]) continue; closed[current]=true;
                    foreach(int next in new[]{current-1,current+1,current-37,current+37})
                    {
                        if(next<0 || next>=count || Math.Abs(next%37-current%37)+Math.Abs(next/37-current/37)!=1 || closed[next] || !Clear(Point(current),Point(next),boxes)) continue;
                        float g=cost[current]+2;
                        if(g<cost[next]) { cost[next]=g; parent[next]=current; open.Add(next); }
                    }
                }
                if(last!=first && parent[last]<0) return null;
                var reverse = new List<Vector3>(); int node=last;
                while(node!=first) { reverse.Add(Point(node)); node=parent[node]; }
                reverse.Add(Point(first)); reverse.Reverse(); raw.AddRange(reverse);
            }
            raw.Add(down);
            var route=new List<Vector3>{up}; int index=0;
            while(index<raw.Count-1)
            {
                int far=raw.Count-1; while(far>index+1 && !Clear(raw[index],raw[far],boxes)) far--;
                route.Add(raw[far]); index=far;
            }
            route.Add(goal); return route;
        }
        public static float Length(Vector3 start, IReadOnlyList<Vector3> route)
        { if(route==null) return float.PositiveInfinity; float sum=0; foreach(var p in route){sum+=Vector3.Distance(start,p);start=p;} return sum; }
    }
    public sealed class RoutePilot
    {
        public List<Vector3> Route {get;private set;}
        private int index;
        private Vector3 goal;
        public void Reset(FlightSimulation sim) { goal=sim.Goal; Route=RoutePlanner.Plan(sim.Start,sim.Goal,sim.Buildings); index=0; }
        public Vector3 Action(FlightSimulation sim)
        {
            if(Route==null) return Vector3.Zero;
            if(goal!=sim.Goal) Reset(sim);
            var target=Route[index];
            if(index<Route.Count-1 && Vector3.Distance(sim.Position,target)<.18f && sim.Velocity.Length()<.2f) target=Route[++index];
            var e=target-sim.Position;
            float horizontalLimit=3f;
            var horizontal=new Vector2(e.X,e.Z)*1.1f;
            if(horizontal.Length()>horizontalLimit) horizontal=Vector2.Normalize(horizontal)*horizontalLimit;
            var desired=new Vector3(horizontal.X,Clamp(e.Y*.9f,index==Route.Count-1?.6f:2f),horizontal.Y);
            return Vector3.Clamp(new Vector3((desired.X-sim.Velocity.X)*1.8f/4,(desired.Y-sim.Velocity.Y)*1.4f/3,(desired.Z-sim.Velocity.Z)*1.8f/4),-Vector3.One,Vector3.One);
        }
        private static float Clamp(float v,float m){return Math.Max(-m,Math.Min(m,v));}
    }
}
