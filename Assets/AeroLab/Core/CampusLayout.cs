using System;
using System.Linq;
using System.Numerics;
namespace AeroLab.Core
{
    // Interactive edits create a detached configuration; running scenarios are never mutated.
    public static class CampusLayout
    {
        public static ScenarioConfig Copy(ScenarioConfig source)
        {
            return new ScenarioConfig {
                Seed=source.Seed,LayoutSeed=source.LayoutSeed,LayoutOrigin=source.LayoutOrigin,
                Duration=source.Duration,Capacity=source.Capacity,Reserve=source.Reserve,ChargeRate=source.ChargeRate,
                MaxPayload=source.MaxPayload,IdlePower=source.IdlePower,PayloadPower=source.PayloadPower,TravelCost=source.TravelCost,
                Depot=source.Depot,Pads=(Vector3[])source.Pads.Clone(),Buildings=(Building[])source.Buildings.Clone(),
                Orders=source.Orders.Select(o=>new OrderSpec{Id=o.Id,Destination=o.Destination,Release=o.Release,Deadline=o.Deadline,Mass=o.Mass}).ToArray(),
                Sensors=new SensorConfig{PositionNoise=source.Sensors.PositionNoise,RangeNoise=source.Sensors.RangeNoise,DropProbability=source.Sensors.DropProbability,DelayTicks=source.Sensors.DelayTicks},
                LandStatus=source.LandStatus,FlightPermission=source.FlightPermission
            };
        }
        public static ScenarioConfig Randomize(ScenarioConfig source,int seed,int stage)
        {
            var result=Copy(source);result.Buildings=ScenarioConfig.Create(seed,false,stage).Buildings;
            result.LayoutSeed=seed;result.LayoutOrigin="randomized";Validate(result);return result;
        }
        public static ScenarioConfig Edit(ScenarioConfig source,int index,float x,float z,float height)
        {
            if(index<0 || index>=source.Buildings.Length)throw new ArgumentException("Chọn một tòa nhà hợp lệ.");
            var result=Copy(source);var half=result.Buildings[index].Half;
            result.Buildings[index]=new Building(new Vector3(x,height/2,z),new Vector3(half.X,height/2,half.Z));
            result.LayoutOrigin="manual";Validate(result);return result;
        }
        public static void Validate(ScenarioConfig config)
        {
            foreach(var b in config.Buildings)
            {
                float height=b.Half.Y*2;
                if(!Finite(b.Center.X)||!Finite(b.Center.Z)||!Finite(height)||height<.5f||height>FlightSimulation.Ceiling)
                    throw new ArgumentException("Chiều cao phải từ 0,5 đến 30 m; tọa độ phải là số hữu hạn.");
                if(Math.Abs(b.Center.Y-b.Half.Y)>.001f)throw new ArgumentException("Chân tòa nhà phải nằm trên nền phẳng.");
                if(Math.Abs(b.Center.X)+b.Half.X>FlightSimulation.Limit || Math.Abs(b.Center.Z)+b.Half.Z>FlightSimulation.Limit)
                    throw new ArgumentException("Toàn bộ tòa nhà phải nằm trong sân 80 × 80 m.");
            }
            for(int i=0;i<config.Buildings.Length;i++)for(int j=0;j<i;j++)
            {
                var a=config.Buildings[i];var b=config.Buildings[j];
                if(Math.Abs(a.Center.X-b.Center.X)<a.Half.X+b.Half.X && Math.Abs(a.Center.Z-b.Center.Z)<a.Half.Z+b.Half.Z)
                    throw new ArgumentException("Các tòa nhà không được chồng lên nhau.");
            }
            try{config.Validate();}
            catch(ArgumentException error)
            {
                if(error.Message=="Obstructed pad")throw new ArgumentException("Tòa nhà che kho/điểm giao hoặc vùng tiếp cận tối thiểu 3,5 m.");
                throw;
            }
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
    }
}
