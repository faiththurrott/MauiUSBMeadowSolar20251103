using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiUSBMeadowSolar20251103
{
    // Represents ONE data point that we will draw on the graph.
    public class SolarGraphPoint
    {
        // The time when this sample was taken
        public DateTime Time { get; set; }

        // Measured solar panel voltage at this time (in volts)
        public double SolarVoltage { get; set; }

        // Measured battery voltage at this time (in volts)
        public double BatteryVoltage { get; set; }
    }

    // Stores a rolling history of samples that the graph can draw.
    public class SolarGraphData
    {
        // The maximum number of points we want to keep in memory.
        // Once we reach this number, older points will be removed.
        public int MaxPoints { get; }

        // SolarGraphDrawable reads from this list when it draws the graph.
        public List<SolarGraphPoint> Points { get; } = new();

        // Constructor: you can optionally pass how many points to keep.
        // Default is 300
        public SolarGraphData(int maxPoints = 300)
        {
            MaxPoints = maxPoints;
        }

        // Add a new sample to the history.
        // We pass in the solar and battery voltages, and this method
        public void AddSample(double solarV, double batteryV)
        {
            // Create a new point and fill in its properties
            var p = new SolarGraphPoint
            {
                Time = DateTime.Now,      // record the time right now
                SolarVoltage = solarV,    // store the solar voltage
                BatteryVoltage = batteryV // store the battery voltage
            };

            // Add this new point to the end of our list
            Points.Add(p);

            // If we have more points than MaxPoints,
            // remove the oldest one (at index 0).
            if (Points.Count > MaxPoints)
                Points.RemoveAt(0);
        }
    }
}
