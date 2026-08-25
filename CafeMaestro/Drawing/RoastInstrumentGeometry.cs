namespace CafeMaestro.Drawing;

public static class RoastInstrumentGeometry
{
    public static double ElapsedSweep(double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0;
        }

        return (elapsedSeconds % 60d) / 60d;
    }

    public static double CoolingProgress(double remainingSeconds, double durationSeconds)
    {
        if (!double.IsFinite(remainingSeconds) || !double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            return 0;
        }

        return Math.Clamp(remainingSeconds / durationSeconds, 0d, 1d);
    }
}
