using System;
using Microsoft.Maui.Graphics;

namespace MauiUSBMeadowSolar20251103
{
    // This class draws the solar/battery graph onto a MAUI canvas.
    public class SolarGraphDrawable : IDrawable
    {
        // The graph data (list of points with time, solar voltage, battery voltage)
        public SolarGraphData Data { get; }

        // You must pass in the data when you create the drawable.
        public SolarGraphDrawable(SolarGraphData data)
        {
            Data = data;
        }

        // This method is called by MAUI whenever the graph needs to be drawn or refreshed.
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // Draw white background over the whole drawing area
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            // If we don't have at least 2 points, show a waiting message and stop.
            if (Data.Points.Count < 2)
            {
                canvas.FontColor = Colors.Gray;
                canvas.DrawString("Waiting for data…", dirtyRect,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                return;
            }

            // Define the plotting area inside the rectangle
            float left = dirtyRect.Left + 40;   // leave space on the left for voltage labels
            float right = dirtyRect.Right - 10; // small margin on the right
            float top = dirtyRect.Top + 10;     // small margin at the top
            float bottom = dirtyRect.Bottom - 30; // leave space at the bottom for x-axis label

            // Fixed voltage range for the Y-axis (0 V to 4 V)
            double vMin = 0;
            double vMax = 4;

            // Local variable for the list of points
            var points = Data.Points;

            // Get the earliest and latest times from the data.
            // ToOADate() converts DateTime to a numeric value that we can subtract.
            double tMin = points[0].Time.ToOADate();          // time of first (oldest) point
            double tMax = points[^1].Time.ToOADate();         // time of last (most recent) point
            double tRange = Math.Max(1e-6, tMax - tMin);      // make sure the range is never zero

            // Draw grid lines and Y-axis labels (0 V to 4 V) 

            canvas.StrokeColor = Colors.LightGray;
            canvas.StrokeSize = 1;

            // Draw 5 horizontal grid lines (0, 1, 2, 3, 4 V)
            for (int i = 0; i <= 4; i++)
            {
                // Calculate the Y position for this grid line (0 at bottom, 4 at top)
                float y = (float)(bottom - (bottom - top) * i / 4.0);

                // Draw a horizontal line across the plot area
                canvas.DrawLine(left, y, right, y);

                // Work out what voltage this line represents
                double v = vMin + (vMax - vMin) * i / 4.0;

                // Draw the voltage label on the left side
                canvas.FontColor = Colors.Gray;
                canvas.DrawString($"{v:0.0} V",
                    0, y - 8, left - 6, 16,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            // Selector: tells us which value to use from each point (SolarVoltage or BatteryVoltage)
            // Color: line color for this series
            void DrawSeries(Func<SolarGraphPoint, double> selector, Color color)
            {
                PathF path = new();

                // Go through every data point and convert it to (x, y) on the screen
                for (int i = 0; i < points.Count; i++)
                {
                    // Normalize time to a 0..1 range between tMin and tMax
                    double tNorm = (points[i].Time.ToOADate() - tMin) / tRange;

                    // Get the selected value (either SolarVoltage or BatteryVoltage)
                    double val = selector(points[i]);

                    // Map the normalized time to an X coordinate on the plot area
                    float x = (float)(left + (right - left) * tNorm);

                    // Map the voltage value to a Y coordinate (vMin at bottom, vMax at top)
                    float y = (float)(bottom - (bottom - top) * (val - vMin) / (vMax - vMin));

                    // Start a new path at the first point, then connect the rest with lines
                    if (i == 0) path.MoveTo(x, y);
                    else path.LineTo(x, y);
                }

                // Draw the line path with the chosen color
                canvas.StrokeColor = color;
                canvas.StrokeSize = 2;
                canvas.DrawPath(path);
            }


            // Solar voltage line = pink
            DrawSeries(p => p.SolarVoltage, Color.FromArgb("#FF91C8"));

            // Battery voltage line = light blue
            DrawSeries(p => p.BatteryVoltage, Color.FromArgb("#8DDCFF"));

            // Draw X-axis label under the graph
            canvas.FontColor = Colors.Gray;
            canvas.DrawString("Time (recent)",
                left, bottom + 4, right - left, 20,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
