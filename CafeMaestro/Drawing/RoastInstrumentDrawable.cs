using Microsoft.Maui.Graphics;

namespace CafeMaestro.Drawing;

public sealed class RoastInstrumentDrawable : IDrawable
{
    public double Progress { get; set; }
    public bool IsPaused { get; set; }
    public bool IsCooling { get; set; }
    public required Color TrackColor { get; init; }
    public required Color RoastColor { get; init; }
    public required Color PausedColor { get; init; }
    public required Color CoolingColor { get; init; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        float radius = Math.Max(0, size / 2f - 14f);
        PointF center = dirtyRect.Center;

        canvas.SaveState();
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = 3;
        canvas.StrokeColor = TrackColor.WithAlpha(IsPaused ? 0.35f : 0.65f);
        for (int index = 0; index < 60; index++)
        {
            double angle = (index / 60d * Math.PI * 2d) - Math.PI / 2d;
            float x = center.X + (float)Math.Cos(angle) * radius;
            float y = center.Y + (float)Math.Sin(angle) * radius;
            canvas.DrawLine(x, y, x, y);
        }

        double progress = Math.Clamp(Progress, 0d, 1d);
        if (IsCooling)
        {
            float diameter = radius * 2f;
            canvas.StrokeSize = 6;
            canvas.StrokeColor = CoolingColor;
            canvas.DrawArc(
                center.X - radius,
                center.Y - radius,
                diameter,
                diameter,
                -90,
                (float)(-90 + 360 * progress),
                true,
                false);
            canvas.RestoreState();
            return;
        }

        double pipAngle = (progress * Math.PI * 2d) - Math.PI / 2d;
        float pipX = center.X + (float)Math.Cos(pipAngle) * radius;
        float pipY = center.Y + (float)Math.Sin(pipAngle) * radius;
        canvas.FillColor = IsPaused ? PausedColor : RoastColor;
        canvas.FillCircle(pipX, pipY, IsPaused ? 5 : 7);
        if (IsPaused)
        {
            canvas.FillColor = TrackColor;
            canvas.FillCircle(pipX, pipY, 2);
        }

        canvas.RestoreState();
    }
}
