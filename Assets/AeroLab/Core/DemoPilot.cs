using System;
using System.Numerics;

namespace AeroLab.Core
{
    /// <summary>Known-layout reference controller. This is NOT a trained AI.</summary>
    public sealed class DemoPilot
    {
        public int Phase { get; private set; }
        public void Reset() { Phase = 0; }

        public Vector3 Action(FlightSimulation sim)
        {
            Vector3 target;
            if (Phase == 0)
            {
                target = new Vector3(sim.Start.X, 16, sim.Start.Z);
                if (Vector3.Distance(sim.Position, target) < 0.35f && sim.Velocity.Length() < 0.3f) Phase = 1;
            }
            else if (Phase == 1)
            {
                target = new Vector3(sim.Goal.X, 16, sim.Goal.Z);
                if (Vector3.Distance(sim.Position, target) < 0.35f && sim.Velocity.Length() < 0.3f) Phase = 2;
            }
            else target = new Vector3(sim.Goal.X, 0.3f, sim.Goal.Z);
            Vector3 error = target - sim.Position;
            Vector3 desired = new Vector3(Clamp(error.X * 1.2f, 5), Clamp(error.Y * 1.1f, 2), Clamp(error.Z * 1.2f, 5));
            if (Phase == 2) desired.Y = Clamp(error.Y * 0.9f, 0.65f);
            return Vector3.Clamp(new Vector3((desired.X - sim.Velocity.X) * 2.5f / 4,
                desired.Y - sim.Velocity.Y, (desired.Z - sim.Velocity.Z) * 2.5f / 4), -Vector3.One, Vector3.One);
        }
        private static float Clamp(float value, float limit) { return Math.Max(-limit, Math.Min(limit, value)); }
    }
}
