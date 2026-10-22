using System;
using System.Collections.Generic;
using System.Numerics;

namespace AeroLab.Core
{
    public enum FlightResult { None, Success, Collision, OutOfBounds, Timeout }

    public readonly struct Building
    {
        public readonly Vector3 Center;
        public readonly Vector3 Half;
        public Building(Vector3 center, Vector3 half) { Center = center; Half = half; }
    }

    /// <summary>
    /// Fixed-attitude, assisted flight at 50 Hz. Pure C#: the Unity game and
    /// future learning adapter must use this exact implementation.
    /// Collisions use swept hull AABBs rather than a second physics engine.
    /// </summary>
    public sealed class FlightSimulation
    {
        public const float Dt = 0.02f;
        public const float Limit = 40f;
        public const float Ceiling = 30f;
        public const int MaxTicks = 3000;
        public const float SensorRange = 30f;
        public const int ObservationSize = 20;
        public static readonly Vector3 Half = new Vector3(1f, 0.5f, 1f);

        private static readonly Vector3[] Directions = {
            Vector3.UnitX, -Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitZ,
            Vector3.Normalize(new Vector3(1, 0, -1)),
            Vector3.Normalize(new Vector3(-1, 0, -1)),
            Vector3.Normalize(new Vector3(1, 0, 1)),
            Vector3.Normalize(new Vector3(-1, 0, 1)),
            Vector3.UnitY, -Vector3.UnitY
        };

        private readonly List<Building> buildings = new List<Building>();
        private int landingTicks;
        public Vector3 Position { get; internal set; }
        public Vector3 Velocity { get; internal set; }
        public Vector3 Start { get; private set; }
        public Vector3 Goal { get; private set; }
        public int Seed { get; private set; }
        public int Ticks { get; internal set; }
        public float Elapsed { get { return Ticks * Dt; } }
        public float LandingProgress { get { return Math.Min(1f, landingTicks * Dt); } }
        public float TotalReward { get; private set; }
        public bool Running { get; private set; }
        public FlightResult Result { get; private set; }
        public IReadOnlyList<Building> Buildings { get { return buildings.AsReadOnly(); } }
        public bool Grounded { get { return Position.Y <= Half.Y + 0.00001f; } }

        public FlightSimulation() { Reset(); }

        public void Reset(int seed = 0, bool obstacles = true)
        {
            Seed = seed;
            // Explicit PRNG: layouts do not depend on System.Random's runtime version.
            uint state = unchecked((uint)seed);
            Vector3 shift = seed == 0 ? Vector3.Zero : new Vector3(Next(ref state), 0, Next(ref state));
            Start = new Vector3(-18, Half.Y, 16) + shift;
            Goal = new Vector3(18, Half.Y, -14) + shift;
            buildings.Clear();
            if (obstacles)
            {
                buildings.Add(new Building(new Vector3(-3, 4, 0) + shift, new Vector3(4, 4, 5)));
                buildings.Add(new Building(new Vector3(10, 6, 8) + shift, new Vector3(3, 6, 4)));
                buildings.Add(new Building(new Vector3(-19, 3, -12) + shift, new Vector3(4, 3, 4)));
                buildings.Add(new Building(new Vector3(27, 4.5f, -2) + shift, new Vector3(3, 4.5f, 4)));
            }
            Position = Start;
            Velocity = Vector3.Zero;
            Ticks = landingTicks = 0;
            TotalReward = 0;
            Result = FlightResult.None;
            Running = false;
        }

        public void Begin() { if (Result == FlightResult.None) Running = true; }

        /// <returns>Incremental reward; zero after termination or before Begin.</returns>
        public float Step(Vector3 action)
        {
            if (!Running || Result != FlightResult.None) return 0;
            if (!Finite(action)) throw new ArgumentException("Action must contain finite numbers.", nameof(action));
            float oldDistance = Vector3.Distance(Position, Goal);
            Vector3 command = Vector3.Clamp(action, -Vector3.One, Vector3.One);
            Vector2 horizontal = LimitLength(new Vector2(command.X, command.Z), 1);
            Vector2 horizontalVelocity = new Vector2(Velocity.X, Velocity.Z);
            horizontalVelocity = horizontal.LengthSquared() > 0.0001f
                ? horizontalVelocity + horizontal * (4f * Dt)
                : MoveTowards(horizontalVelocity, Vector2.Zero, 4f * Dt);
            horizontalVelocity = LimitLength(horizontalVelocity, 8);
            float vertical = Math.Abs(command.Y) > 0.0001f
                ? Clamp(Velocity.Y + command.Y * 3f * Dt, -3, 3)
                : MoveTowards(Velocity.Y, 0, 3f * Dt);
            Velocity = new Vector3(horizontalVelocity.X, vertical, horizontalVelocity.Y);
            Vector3 next = Position + Velocity * Dt;
            Ticks++;
            bool hit = false;
            Vector3 displacement = next - Position;
            float distance = displacement.Length();
            Vector3 direction = distance > 0 ? displacement / distance : Vector3.Zero;
            foreach (Building building in buildings)
            {
                Vector3 extent = building.Half + Half;
                if (RayBox(Position, direction, building.Center - extent, building.Center + extent, distance) >= 0)
                { hit = true; break; }
            }
            if (next.Y <= Half.Y)
            {
                // Test impact speed before resolving contact.
                if (Velocity.Y < -1) hit = true;
                next.Y = Half.Y;
                Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
            }
            Position = next;
            bool outside = Math.Abs(Position.X) + Half.X > Limit
                || Math.Abs(Position.Z) + Half.Z > Limit || Position.Y + Half.Y > Ceiling;
            bool stable = OnTarget && new Vector2(Velocity.X, Velocity.Z).Length() <= 0.5f && Math.Abs(Velocity.Y) <= 0.5f;
            landingTicks = stable ? landingTicks + 1 : 0;
            if (hit) Result = FlightResult.Collision;
            else if (outside) Result = FlightResult.OutOfBounds;
            else if (landingTicks >= 50) Result = FlightResult.Success;
            else if (Ticks >= MaxTicks) Result = FlightResult.Timeout;
            float reward = (oldDistance - Vector3.Distance(Position, Goal)) * 0.1f - Dt * 0.02f;
            if (Result == FlightResult.Success) reward += 10;
            else if (Result == FlightResult.Collision || Result == FlightResult.OutOfBounds) reward -= 5;
            else if (Result == FlightResult.Timeout) reward -= 1;
            TotalReward += reward;
            if (Result != FlightResult.None) Running = false;
            return reward;
        }

        public bool OnTarget
        {
            get { return Grounded && Math.Abs(Position.X - Goal.X) + Half.X <= 3 && Math.Abs(Position.Z - Goal.Z) + Half.Z <= 3; }
        }

        /// <summary>Normalized ranges: +X,-X,-Z,+Z, diagonals, up, down; measured from the hull.</summary>
        public float[] Sensors()
        {
            float[] readings = new float[Directions.Length];
            for (int i = 0; i < Directions.Length; i++)
            {
                Vector3 direction = Directions[i];
                float distance = SensorRange;
                for (int axis = 0; axis < 3; axis++)
                {
                    float component = At(direction, axis);
                    if (Math.Abs(component) < 0.000001f) continue;
                    float lower = axis == 1 ? Half.Y : -Limit + At(Half, axis);
                    float upper = axis == 1 ? Ceiling - Half.Y : Limit - At(Half, axis);
                    float boundary = component > 0 ? upper : lower;
                    distance = Math.Min(distance, Math.Max(0, (boundary - At(Position, axis)) / component));
                }
                foreach (Building building in buildings)
                {
                    Vector3 extent = building.Half + Half;
                    float entry = RayBox(Position, direction, building.Center - extent, building.Center + extent, SensorRange);
                    if (entry >= 0) distance = Math.Min(distance, entry);
                }
                readings[i] = Clamp(distance / SensorRange, 0, 1);
            }
            return readings;
        }

        /// <summary>
        /// Goal delta XYZ /80, velocity XYZ /[8,3,8], altitude /30, grounded,
        /// ten ranges, time remaining /60, landing hold /1. Exactly 20 floats.
        /// </summary>
        public float[] Observe()
        {
            Vector3 relative = (Goal - Position) / 80;
            float[] values = new float[ObservationSize];
            values[0] = relative.X; values[1] = relative.Y; values[2] = relative.Z;
            values[3] = Velocity.X / 8; values[4] = Velocity.Y / 3; values[5] = Velocity.Z / 8;
            values[6] = (Position.Y - Half.Y) / Ceiling; values[7] = Grounded ? 1 : 0;
            Array.Copy(Sensors(), 0, values, 8, Directions.Length);
            values[18] = 1 - Elapsed / 60; values[19] = LandingProgress;
            return values;
        }

        internal static float RayBox(Vector3 origin, Vector3 direction, Vector3 lower, Vector3 upper, float maxDistance)
        {
            float near = 0, far = maxDistance;
            for (int axis = 0; axis < 3; axis++)
            {
                float component = At(direction, axis), coordinate = At(origin, axis);
                if (Math.Abs(component) < 0.000001f)
                {
                    if (coordinate < At(lower, axis) || coordinate > At(upper, axis)) return -1;
                }
                else
                {
                    float a = (At(lower, axis) - coordinate) / component;
                    float b = (At(upper, axis) - coordinate) / component;
                    near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
                    if (near > far) return -1;
                }
            }
            return near;
        }

        private static float At(Vector3 v, int axis) { return axis == 0 ? v.X : axis == 1 ? v.Y : v.Z; }
        internal static float Clamp(float v, float min, float max) { return Math.Max(min, Math.Min(max, v)); }
        private static Vector2 LimitLength(Vector2 value, float limit) { return value.LengthSquared() > limit * limit ? Vector2.Normalize(value) * limit : value; }
        private static Vector2 MoveTowards(Vector2 current, Vector2 target, float delta)
        { Vector2 difference = target - current; return difference.Length() <= delta ? target : current + Vector2.Normalize(difference) * delta; }
        private static float MoveTowards(float current, float target, float delta)
        { return Math.Abs(target - current) <= delta ? target : current + Math.Sign(target - current) * delta; }
        private static bool Finite(Vector3 v)
        { return !(float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z) || float.IsInfinity(v.X) || float.IsInfinity(v.Y) || float.IsInfinity(v.Z)); }
        private static float Next(ref uint state)
        {
            if (state == 0) state = 0x9e3779b9;
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return state / (float)uint.MaxValue * 4 - 2;
        }
    }
}
