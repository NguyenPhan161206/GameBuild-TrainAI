using System;
using System.Numerics;
namespace AeroLab.Core
{
    // Vector-only feedback teacher. Its 26 m cruise prior is valid only for this
    // generator (buildings below 24 m and open approaches to the fixed pads).
    // This is a scripted baseline, never labelled as a trained policy.
    public static class ClearancePilot
    {
        public static Vector3 Action(float[] observation)
        {
            float x=observation[0]*80,z=observation[2]*80;
            float altitude=observation[6]*30;
            float distance=(float)Math.Sqrt(x*x+z*z);
            float targetClearance=-.2f+25.7f*Clamp((distance-1)/8,0,1);
            float error=targetClearance-altitude;
            float gate=Clamp(1-Math.Abs(error)/3,0,1);
            var horizontal=new Vector2(x,z)*1.1f;
            if(horizontal.Length()>3)horizontal=Vector2.Normalize(horizontal)*3;
            horizontal*=gate;
            float vertical=Clamp(error*.9f,altitude<3?-.6f:-2,2);
            return Vector3.Clamp(new Vector3((horizontal.X-observation[3]*8)*1.8f/4,
                (vertical-observation[4]*3)*1.4f/3,(horizontal.Y-observation[5]*8)*1.8f/4),-Vector3.One,Vector3.One);
        }
        private static float Clamp(float x,float low,float high){return Math.Max(low,Math.Min(high,x));}
    }
}
