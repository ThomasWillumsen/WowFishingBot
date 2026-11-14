using System.Diagnostics;

namespace DeskHelper.Infrastructure;

/// <summary>
/// Human-like timing: right-skewed delays (most are short, a few are long), a per-run pace
/// and a slow slowdown over the session, so delays don't share one fixed statistical fingerprint.
/// </summary>
internal static class RandomDelay
{
    // where the typical delay sits inside [min, max] (0 = min, 1 = max) and how skewed the tail is
    private const double MedianPosition = 0.3;
    private const double Sigma = 0.55;

    private const double FatiguePerHour = 0.12;
    private const double MaxFatigue = 0.25;

    private static readonly Stopwatch SessionClock = Stopwatch.StartNew();
    private static readonly double Pace = 0.9 + Random.Shared.NextDouble() * 0.3;

    public static void Sleep(int minMs, int maxMs) => Thread.Sleep(Next(minMs, maxMs));

    public static int Next(int minMs, int maxMs)
    {
        double position;
        do
        {
            position = Math.Exp(Math.Log(MedianPosition) + Sigma * Gaussian());
        }
        while (position > 1);

        return (int)Math.Round(Scaled(minMs + position * (maxMs - minMs)));
    }

    /// <summary>Applies this run's pace and the accumulated fatigue to a duration.</summary>
    public static double Scaled(double ms)
    {
        var fatigue = 1 + Math.Min(FatiguePerHour * SessionClock.Elapsed.TotalHours, MaxFatigue);
        return ms * Pace * fatigue;
    }

    public static bool Chance(double probability) => Random.Shared.NextDouble() < probability;

    public static double Gaussian(double mean = 0, double standardDeviation = 1)
    {
        // Box-Muller transform; 1 - NextDouble() keeps the logarithm's argument above zero
        var u1 = 1 - Random.Shared.NextDouble();
        var u2 = Random.Shared.NextDouble();
        return mean + standardDeviation * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
