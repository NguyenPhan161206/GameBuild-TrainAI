using System;
using System.Numerics;
namespace AeroLab.Core
{
    [Serializable] public sealed class LegReport
    {
        public int Leg, Trip, OrderId;
        public bool Returning;
        public Vector3 Start, Goal, FinalPosition, FinalVelocity;
        public float Started, Ended, BatteryStart, BatteryEnd, Payload, Distance;
        public string Result;
    }
    [Serializable] public sealed class DeliveryTrace
    {
        public string Event, Phase;
        public int Tick, Leg, Trip, OrderId;
        public Vector3 Position, Velocity, Goal, Command;
        public float Battery, Payload;
        public float[] Observation;
    }
}
