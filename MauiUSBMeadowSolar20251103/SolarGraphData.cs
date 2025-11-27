using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiUSBMeadowSolar20251103
{
    // One sample for the graph
    public class SolarGraphPoint
    {
        public DateTime Time { get; set; }          // when we sampled
        public double SolarVoltage { get; set; }    // in volts
        public double BatteryVoltage { get; set; }  // in volts
    }

    // Rolling history of samples (for plotting)
    public class SolarGraphData
    {
        // Maximum number of points we keep (older ones get dropped)
        public int MaxPoints { get; }

        // List of all the points we’ve kept
        public List<SolarGraphPoint> Points { get; } = new();

        public SolarGraphData(int maxPoints = 300)
        {
            MaxPoints = maxPoints;
        }

        public void AddSample(double solarV, double batteryV)
        {
            var p = new SolarGraphPoint
            {
                Time = DateTime.Now,
                SolarVoltage = solarV,
                BatteryVoltage = batteryV
            };

            Points.Add(p);

            // keep list trimmed so it doesn’t grow forever
            if (Points.Count > MaxPoints)
                Points.RemoveAt(0);
        }
    }
}