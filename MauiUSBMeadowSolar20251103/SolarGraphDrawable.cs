using System;
using Microsoft.Maui.Graphics;

namespace MauiUSBMeadowSolar20251103
{
    public class SolarGraphDrawable : IDrawable
    {
        public SolarGraphData Data { get; }

        public SolarGraphDrawable(SolarGraphData data)
        {
            Data = data;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            // Background
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            if (Data.Points.Count < 2)
            {
                canvas.FontColor = Colors.Gray;
                canvas.DrawString("Waiting for data…", dirtyRect,
                    HorizontalAlignment.Center, VerticalAlignment.Center);
                return;
            }

            float left = dirtyRect.Left + 40;
            float right = dirtyRect.Right - 10;
            float top = dirtyRect.Top + 10;
            float bottom = dirtyRect.Bottom - 30;

            double vMin = 0;
            double vMax = 4;

            var points = Data.Points;
            double tMin = points[0].Time.ToOADate();
            double tMax = points[^1].Time.ToOADate();
            double tRange = Math.Max(1e-6, tMax - tMin);

            // --- grid lines & axis labels ---
            canvas.StrokeColor = Colors.LightGray;
            canvas.StrokeSize = 1;

            for (int i = 0; i <= 4; i++)
            {
                float y = (float)(bottom - (bottom - top) * i / 4.0);
                canvas.DrawLine(left, y, right, y);

                double v = vMin + (vMax - vMin) * i / 4.0;
                canvas.FontColor = Colors.Gray;
                canvas.DrawString($"{v:0.0} V",
                    0, y - 8, left - 6, 16,
                    HorizontalAlignment.Right, VerticalAlignment.Center);
            }

            // Helper for drawing one series
            void DrawSeries(Func<SolarGraphPoint, double> selector, Color color)
            {
                PathF path = new();

                for (int i = 0; i < points.Count; i++)
                {
                    double tNorm = (points[i].Time.ToOADate() - tMin) / tRange;
                    double val = selector(points[i]);

                    float x = (float)(left + (right - left) * tNorm);
                    float y = (float)(bottom - (bottom - top) * (val - vMin) / (vMax - vMin));

                    if (i == 0) path.MoveTo(x, y);
                    else path.LineTo(x, y);
                }

                canvas.StrokeColor = color;
                canvas.StrokeSize = 2;
                canvas.DrawPath(path);
            }

            // Solar = pink, Battery = blue
            DrawSeries(p => p.SolarVoltage, Color.FromArgb("#FF91C8"));
            DrawSeries(p => p.BatteryVoltage, Color.FromArgb("#8DDCFF"));

            // X-axis label
            canvas.FontColor = Colors.Gray;
            canvas.DrawString("Time (recent)",
                left, bottom + 4, right - left, 20,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
