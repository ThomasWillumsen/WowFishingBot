using System.Diagnostics;
using System.Drawing;
using DeskHelper.Infrastructure;

namespace DeskHelper.Input;

internal static class CursorMover
{
    private const double OvershootChance = 0.35;
    private const int OvershootMinDistance = 150;

    /// <summary>
    /// Moves the cursor to <paramref name="target"/> like a hand would: the duration follows Fitts's law
    /// (farther and smaller targets take longer), the speed rises and falls smoothly, the path is curved,
    /// and long moves sometimes overshoot slightly and then correct.
    /// </summary>
    /// <param name="targetWidth">Approximate size of the target in pixels. Bigger targets are approached faster.</param>
    public static void MoveTo(Point target, int targetWidth = 40)
    {
        NativeMethods.GetCursorPos(out var nativeStart);
        Point start = nativeStart;
        var distance = Distance(start, target);
        Log.Debug($"Moving mouse from ({start.X}, {start.Y}) to ({target.X}, {target.Y}), distance {distance:F0}px");

        if (distance < 2)
            return;

        var duration = MovementTimeMs(distance, targetWidth);

        if (distance > OvershootMinDistance && RandomDelay.Chance(OvershootChance))
        {
            var overshootPoint = OvershootPoint(start, target, distance);
            MoveSegment(start, overshootPoint, duration * 0.9, curve: RandomDelay.Gaussian(0.07, 0.03));

            // a short pause while the "eyes" notice the miss, then a small corrective move
            RandomDelay.Sleep(30, 90);
            NativeMethods.GetCursorPos(out var nativeCurrent);
            Point current = nativeCurrent;
            MoveSegment(current, target, RandomDelay.Next(90, 180), curve: 0.01);
            return;
        }

        MoveSegment(start, target, duration, curve: RandomDelay.Gaussian(0.07, 0.03));
    }

    private static double MovementTimeMs(double distance, int targetWidth)
    {
        var fitts = 150 + 110 * Math.Log2(distance / targetWidth + 1);
        var variation = Math.Exp(RandomDelay.Gaussian(0, 0.15));
        return Math.Clamp(RandomDelay.Scaled(fitts * variation), 180, 1100);
    }

    private static Point OvershootPoint(Point start, Point target, double distance)
    {
        var dirX = (target.X - start.X) / distance;
        var dirY = (target.Y - start.Y) / distance;

        var overshoot = Math.Clamp(distance * (0.02 + Random.Shared.NextDouble() * 0.04), 3, 25);
        var sideways = RandomDelay.Gaussian(0, 2);

        return new Point(
            (int)Math.Round(target.X + dirX * overshoot - dirY * sideways),
            (int)Math.Round(target.Y + dirY * overshoot + dirX * sideways));
    }

    /// <summary>Moves along a cubic Bezier curve that bows sideways by <paramref name="curve"/> times the distance.</summary>
    private static void MoveSegment(Point from, Point to, double durationMs, double curve)
    {
        var distance = Distance(from, to);
        if (distance < 1)
        {
            NativeMethods.SetCursorPos(to.X, to.Y);
            return;
        }

        // unit vector perpendicular to the straight line
        var normalX = -(to.Y - from.Y) / distance;
        var normalY = (to.X - from.X) / distance;

        var side = Random.Shared.Next(2) == 0 ? -1 : 1;
        var bend1 = side * distance * curve * (0.5 + Random.Shared.NextDouble());
        var bend2 = side * distance * curve * Random.Shared.NextDouble();

        var control1 = (X: from.X + (to.X - from.X) * 0.33 + normalX * bend1, Y: from.Y + (to.Y - from.Y) * 0.33 + normalY * bend1);
        var control2 = (X: from.X + (to.X - from.X) * 0.66 + normalX * bend2, Y: from.Y + (to.Y - from.Y) * 0.66 + normalY * bend2);

        var clock = Stopwatch.StartNew();
        var last = from;

        while (clock.Elapsed.TotalMilliseconds < durationMs)
        {
            var t = clock.Elapsed.TotalMilliseconds / durationMs;
            var s = MinimumJerk(t);
            var u = 1 - s;

            var x = u * u * u * from.X + 3 * u * u * s * control1.X + 3 * u * s * s * control2.X + s * s * s * to.X;
            var y = u * u * u * from.Y + 3 * u * u * s * control1.Y + 3 * u * s * s * control2.Y + s * s * s * to.Y;

            // hand tremor, fading to zero at both ends so the segment still starts and ends exactly where intended
            var tremor = Math.Sin(Math.PI * t);
            var point = new Point(
                (int)Math.Round(x + RandomDelay.Gaussian(0, 0.4) * tremor),
                (int)Math.Round(y + RandomDelay.Gaussian(0, 0.4) * tremor));

            if (point != last)
            {
                NativeMethods.SetCursorPos(point.X, point.Y);
                last = point;
            }

            // roughly a 100-160 Hz mouse report rate
            Thread.Sleep(Random.Shared.Next(6, 10));
        }

        NativeMethods.SetCursorPos(to.X, to.Y);
    }

    /// <summary>Smooth 0 → 1 profile with zero speed and acceleration at both ends.</summary>
    private static double MinimumJerk(double t) => t * t * t * (10 + t * (-15 + 6 * t));

    private static double Distance(Point a, Point b) => Math.Sqrt((double)(b.X - a.X) * (b.X - a.X) + (double)(b.Y - a.Y) * (b.Y - a.Y));
}
