using System;
using System.Collections.Generic;
using System.Numerics;

namespace AeroLab.Core
{
    [Serializable] public sealed class OrderSpec
    {
        public int Id, Destination;
        public float Release, Deadline, Mass;
    }
    [Serializable] public sealed class SensorConfig
    {
        public float PositionNoise, RangeNoise, DropProbability;
        public int DelayTicks;
        public static SensorConfig Noisy() { return new SensorConfig { PositionNoise = 0.15f, RangeNoise = 0.1f, DropProbability = 0.01f, DelayTicks = 5 }; }
    }
    [Serializable] public sealed class ScenarioConfig
    {
        public const string Version = "delivery-v3";
        public int Seed;
        public int LayoutSeed;
        public string LayoutOrigin = "generated";
        public float Duration = 900, Capacity = 100, Reserve = 12, ChargeRate = 1;
        public float MaxPayload = 2, IdlePower = 0.12f, PayloadPower = 0.04f, TravelCost = 0.008f;
        public Vector3 Depot;
        public Vector3[] Pads;
        public Building[] Buildings;
        public OrderSpec[] Orders;
        public SensorConfig Sensors = new SensorConfig();
        public string LandStatus = "unverified", FlightPermission = "unverified";
        public static ScenarioConfig Create(int seed = 0, bool noisy = false, int stage = 3)
        {
            var rng = new SeedRandom(seed);
            var c = new ScenarioConfig { Seed = seed, LayoutSeed = seed, Sensors = noisy ? SensorConfig.Noisy() : new SensorConfig() };
            c.Depot = new Vector3(-30, 0.5f, -30);
            c.Pads = new[] { new Vector3(28, .5f, -28), new Vector3(28, .5f, 28), new Vector3(-28, .5f, 28), new Vector3(0, .5f, 30) };
            var boxes = new List<Building>();
            if (stage > 0)
                for (int i = 0; i < 6; i++)
                {
                    // Independent size, height and center changes, not a translated fixed map.
                    float x = (i % 3 - 1) * 15 + rng.Signed() * 3;
                    float z = (i / 3 == 0 ? -9 : 10) + rng.Signed() * 3;
                    float h = 5 + rng.NextFloat() * 19;
                    boxes.Add(new Building(new Vector3(x, h / 2, z), new Vector3(2 + rng.NextFloat() * 2, h / 2, 2 + rng.NextFloat() * 2)));
                }
            c.Buildings = boxes.ToArray();
            c.Orders = new OrderSpec[stage < 2 ? 1 : 12];
            for (int i = 0; i < c.Orders.Length; i++)
            {
                float release = i == 0 ? 0 : i * 32 + rng.NextFloat() * 15;
                c.Orders[i] = new OrderSpec { Id = i, Destination = (int)(rng.NextFloat() * 4) % 4, Release = release, Deadline = release + 110 + rng.NextFloat() * 90, Mass = .2f + rng.NextFloat() * 1.5f };
            }
            if (stage < 2) c.Duration = 240;
            return c;
        }
        public void Validate()
        {
            if (!Positive(Duration) || !Positive(Capacity) || !Positive(Reserve) || Reserve >= Capacity || !Positive(ChargeRate) || !Positive(MaxPayload)
                || !Positive(IdlePower) || !Nonnegative(PayloadPower) || !Nonnegative(TravelCost)) throw new ArgumentException("Invalid energy/time configuration");
            if (Sensors == null || !Nonnegative(Sensors.PositionNoise) || !Nonnegative(Sensors.RangeNoise) || !Nonnegative(Sensors.DropProbability) || Sensors.DropProbability > 1 || Sensors.DelayTicks < 0 || Sensors.DelayTicks > 100) throw new ArgumentException("Invalid sensor configuration");
            if (Pads == null || Pads.Length != 4 || Buildings == null || Orders == null) throw new ArgumentException("Expected four pads and a layout");
            var points = new List<Vector3> { Depot }; points.AddRange(Pads);
            foreach (var b in Buildings)
                if (!Finite(b.Center) || !Finite(b.Half) || b.Half.X <= 0 || b.Half.Y <= 0 || b.Half.Z <= 0) throw new ArgumentException("Invalid building");
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (!Finite(p) || Math.Abs(p.Y - .5f) > .001f || Math.Abs(p.X) > 35 || Math.Abs(p.Z) > 35) throw new ArgumentException("Invalid pad");
                for (int j = 0; j < i; j++) if (Vector3.Distance(p, points[j]) < 7) throw new ArgumentException("Overlapping pads");
                foreach (var b in Buildings)
                    if (Math.Abs(p.X - b.Center.X) < b.Half.X + 3.5f && Math.Abs(p.Z - b.Center.Z) < b.Half.Z + 3.5f) throw new ArgumentException("Obstructed pad");
            }
            var ids = new HashSet<int>();
            foreach (var o in Orders)
                if (o == null || !ids.Add(o.Id) || o.Destination < 0 || o.Destination >= 4 || !Nonnegative(o.Release) || !Positive(o.Deadline) || o.Deadline < o.Release || !Positive(o.Mass)) throw new ArgumentException("Invalid order");
        }
        private static bool Positive(float x) { return Nonnegative(x) && x > 0; }
        private static bool Nonnegative(float x) { return !float.IsNaN(x) && !float.IsInfinity(x) && x >= 0; }
        private static bool Finite(Vector3 x) { return !float.IsNaN(x.X+x.Y+x.Z) && !float.IsInfinity(x.X+x.Y+x.Z); }
    }
    public sealed class SeedRandom
    {
        private uint state;
        public SeedRandom(int seed) { state = unchecked((uint)seed) ^ 0x9e3779b9u; }
        public float NextFloat() { state = unchecked(1664525u * state + 1013904223u); return (state >> 8) / 16777216f; }
        public float Signed() { return NextFloat() * 2 - 1; }
    }
}
